using System;
using System.Collections.Generic;
using ImGuiNET;
using Kf2;
using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

namespace Lexer.Kf2.HitFeedback;

public sealed class HitFeedbackMod : IMod
{
    const uint Creatures = 0x8016C544, Definitions = 0x80172624;
    const int Count = 200, Stride = 0x7C;
    const string DurationSetting = "lexer.kf2.hitfeedback.maximumseconds";
    const string RedSetting = "lexer.kf2.hitfeedback.redintensity";
    static float _maximumSeconds, _redPercent;
    readonly record struct Flash(long Started, long Ends, float MissingHp);
    static readonly Flash[] Flashes = new Flash[Count];
    static readonly Stack<(uint Record, int Before, int Maximum)> Hits = new();
    struct DrawMark
    {
        public int Serial, Main, Mirror;
        public float Strength;
    }
    static readonly Stack<DrawMark> Draws = new();
    static bool _loaded;

    public void OnLoad()
    {
        float saved = Runtime.View.GetFloat(DurationSetting, 0.6f);
        _maximumSeconds = float.IsFinite(saved) ? Math.Clamp(saved, 0f, 5f) : 0.6f;
        saved = Runtime.View.GetFloat(RedSetting, 50f);
        _redPercent = float.IsFinite(saved) ? Math.Clamp(saved, 0f, 100f) : 50f;
        Clear();
        _loaded = true;
        Event.AddListener<OverlayLoadedEvent>(Overlay);
    }

    public void OnUnload()
    {
        _loaded = false;
        Event.RemoveListener<OverlayLoadedEvent>(Overlay);
        Clear();
    }

    public void DrawSettings()
    {
        if (ImGui.SliderFloat("Maximum flash duration (seconds)", ref _maximumSeconds, 0f, 5f, "%.2f", ImGuiSliderFlags.AlwaysClamp))
        {
            _maximumSeconds = float.IsFinite(_maximumSeconds) ? Math.Clamp(_maximumSeconds, 0f, 5f) : 0.6f;
            Runtime.View.SetFloat(DurationSetting, _maximumSeconds);
            Runtime.SaveView();
        }
        if (ImGui.SliderFloat("Red intensity (%)", ref _redPercent, 0f, 100f, "%.0f%%", ImGuiSliderFlags.AlwaysClamp))
        {
            _redPercent = float.IsFinite(_redPercent) ? Math.Clamp(_redPercent, 0f, 100f) : 50f;
            Runtime.View.SetFloat(RedSetting, _redPercent);
            Runtime.SaveView();
        }
        if (!RetainedScene.MainView || !RetainedScene.Supported)
            ImGui.TextDisabled("Enable GPU geometry and model meshes for Hit Feedback.");
    }

    static int Slot(uint record) => record >= Creatures && record < Creatures + Count * Stride
        && (record - Creatures) % Stride == 0 ? (int)((record - Creatures) / Stride) : -1;

    static uint DamageRecord(IMemory m, uint record)
    {
        if (Slot(record) < 0) return 0;
        if (m.ReadU8(record) != 3) return record;
        int parent = (short)m.ReadU16(record + 0x22);
        return parent >= 0 && parent < Count ? Creatures + (uint)parent * Stride : 0;
    }

    static void Clear()
    {
        Array.Clear(Flashes);
        Hits.Clear();
        Draws.Clear();
    }

    static void Overlay(OverlayLoadedEvent e) => Clear();

    // Native damage resolves linked body parts onto their parent before subtracting HP.
    [PreHook("game", Address = 0x8003A9CC)]
    public static void BeforeDamage(CpuContext c, IMemory m)
    {
        uint index = c.A0 & 0xFFFF;
        uint record = DamageRecord(m, index < Count ? Creatures + index * Stride : 0);
        int maximum = record == 0 ? 0 : m.ReadU16(Definitions + m.ReadU8(record + 2) * 0x78u + 0x1A);
        Hits.Push((record, record == 0 ? 0 : m.ReadU16(record + 0x1A), maximum));
    }

    [PostHook("game", Address = 0x8003A9CC)]
    public static void AfterDamage(CpuContext c, IMemory m)
    {
        if (Hits.Count == 0) return;
        var hit = Hits.Pop();
        int slot = Slot(hit.Record);
        if (slot < 0 || hit.Maximum <= 0) return;
        int remaining = m.ReadU16(hit.Record + 0x1A);
        int lost = hit.Before - remaining;
        if (lost <= 0) return;
        long duration = (long)(Math.Clamp((double)lost / hit.Maximum, 0, 1) * _maximumSeconds * 1000);
        long now = Environment.TickCount64;
        // A new hit interrupts the previous pulse: no additive intensity or
        // inherited lifetime. Each pulse obeys this hit's own HP formulas.
        Flashes[slot] = duration > 0
            ? new Flash(now, now + duration, Math.Clamp(1f - (float)remaining / hit.Maximum, 0f, 1f))
            : default;
    }

    // The spawn routine copies this definition's +0x1A maximum into the creature.
    [PreHook("game", Address = 0x80039AFC)]
    public static void Spawn(CpuContext c, IMemory m)
    {
        int slot = Slot(c.A0);
        if (slot >= 0) Flashes[slot] = default;
    }

    [PostHook("game", Address = 0x8004A040)]
    [PreHook("game", Address = 0x8002A264)] // native player death
    [PreHook("game", Address = 0x80025A94)] // native full status reset/respawn
    public static void Loaded(CpuContext c, IMemory m) => Clear();

    static float Intensity(int slot, long now)
    {
        Flash flash = Flashes[slot];
        if (_redPercent <= 0 || _maximumSeconds <= 0 || now >= flash.Ends || flash.Ends <= flash.Started) return 0;
        float remaining = Math.Clamp((float)(flash.Ends - now) / (flash.Ends - flash.Started), 0f, 1f);
        return flash.MissingHp * (_redPercent / 100f) * remaining;
    }

    // Snapshot only this draw's newly captured instances. An inactive mark still
    // balances nested callbacks and prevents an outer creature's pulse coloring
    // an unrelated nested object, menu preview or first-person arm.
    [PreHook("game", Address = 0x80032588)]
    public static void BeforeModel(CpuContext c, IMemory m)
    {
        if (Draws.Count != 0 && Draws.Peek().Serial != RetainedScene.Serial) Draws.Clear();
        if (Draws.Count != 0)
        {
            var parent = Draws.Pop();
            Flush(ref parent);
            Draws.Push(parent);
        }
        int serial = RetainedScene.Serial;
        var frame = RetainedScene.Find(serial);
        int slot = _loaded && RetainedScene.MainView && RetainedScene.Supported && (ModelWalk.InWalk || PlanarWalk.Replaying)
            && ModelWalk.SubmitKind == ModelKind.Creature ? Slot(DamageRecord(m, ModelWalk.SubmitRecord)) : -1;
        Draws.Push(new DrawMark
        {
            Serial = frame == null ? -1 : serial,
            Main = frame?.Instances.Count ?? 0, Mirror = frame?.MirrorInstances.Count ?? 0,
            Strength = slot < 0 ? 0 : Intensity(slot, Environment.TickCount64),
        });
    }

    [PostHook("game", Address = 0x80032588)]
    public static void AfterModel(CpuContext c, IMemory m)
    {
        if (Draws.Count == 0) return;
        var own = Draws.Pop();
        Flush(ref own);
        if (Draws.Count == 0) return;
        // The child's complete ranges belong to the child, including when inert.
        var parent = Draws.Pop();
        var frame = RetainedScene.Find(parent.Serial);
        if (frame != null && parent.Serial == RetainedScene.Serial)
        { parent.Main = frame.Instances.Count; parent.Mirror = frame.MirrorInstances.Count; }
        Draws.Push(parent);
    }

    static void Flush(ref DrawMark mark)
    {
        if (mark.Serial != RetainedScene.Serial) return;
        var frame = RetainedScene.Find(mark.Serial);
        if (frame == null) return;
        Mix(frame.Instances, mark.Main, mark.Strength);
        Mix(frame.MirrorInstances, mark.Mirror, mark.Strength);
        mark.Main = frame.Instances.Count; mark.Mirror = frame.MirrorInstances.Count;
    }

    static void Mix(List<RetainedScene.ModelInstance> instances, int first, float strength)
    {
        if (!_loaded || _redPercent <= 0 || _maximumSeconds <= 0 || strength <= 0 || !float.IsFinite(strength)) return;
        for (int i = Math.Max(first, 0); i < instances.Count; i++)
        {
            var instance = instances[i];
            instance.ColourMixRgb = 0x0000FF;
            instance.ColourMix = Math.Clamp(strength, 0f, 1f);
            instances[i] = instance;
        }
    }
}
