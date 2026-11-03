using System;
using System.Collections.Generic;
using ImGuiNET;
using Kf2;
using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Hardware;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

namespace Lexer.Kf2.HitFeedback;

public sealed class HitFeedbackMod : IMod
{
    const uint Creatures = 0x8016C544, Definitions = 0x80172624, LightColour = 0x8006E604;
    const int Count = 200, Stride = 0x7C;
    const string DurationSetting = "lexer.kf2.hitfeedback.maximumseconds";
    static float _maximumSeconds;
    static readonly long[] Until = new long[Count];
    static readonly Stack<(uint Record, int Before, int Maximum)> Hits = new();
    static readonly uint[] Colours = new uint[8];
    static uint _rgbc, _gteRgbc;
    static bool _tinted;

    public void OnLoad()
    {
        float saved = Runtime.View.GetFloat(DurationSetting, 0.6f);
        _maximumSeconds = float.IsFinite(saved) ? Math.Clamp(saved, 0f, 5f) : 0.6f;
        Clear();
        Event.AddListener<OverlayLoadedEvent>(Overlay);
    }

    public void OnUnload()
    {
        Event.RemoveListener<OverlayLoadedEvent>(Overlay);
        Clear();
    }

    public void DrawSettings()
    {
        if (ImGui.SliderFloat("Maximum flash duration (seconds)", ref _maximumSeconds, 0f, 5f, "%.2f"))
        {
            Runtime.View.SetFloat(DurationSetting, _maximumSeconds);
            Runtime.SaveView();
        }
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
        if (Runtime.Mem != null) RestoreLighting(Runtime.Mem);
        Array.Clear(Until);
        Hits.Clear();
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
        int lost = hit.Before - m.ReadU16(hit.Record + 0x1A);
        if (lost <= 0) return;
        long duration = (long)(Math.Clamp((double)lost / hit.Maximum, 0, 1) * _maximumSeconds * 1000);
        Until[slot] = Math.Max(Until[slot], Environment.TickCount64 + duration);
    }

    // The spawn routine copies this definition's +0x1A maximum into the creature.
    [PreHook("game", Address = 0x80039AFC)]
    public static void Spawn(CpuContext c, IMemory m)
    {
        int slot = Slot(c.A0);
        if (slot >= 0) Until[slot] = 0;
    }

    [PostHook("game", Address = 0x8004A040)]
    public static void Loaded(CpuContext c, IMemory m) => Clear();

    // Every model submit resolves its model at this call site after installing its
    // lighting and before building GPU instances or the original polygon packets.
    [PostHook("game", Address = 0x80034834)]
    public static void Tint(CpuContext c, IMemory m)
    {
        if (_tinted || c.RA != 0x800329A0 || ModelWalk.SubmitKind != ModelKind.Creature) return;
        int slot = Slot(DamageRecord(m, ModelWalk.SubmitRecord));
        if (slot < 0 || Until[slot] <= Environment.TickCount64) return;
        for (int i = 0; i < Colours.Length; i++) Colours[i] = Gte.ReadControl(13 + i);
        _rgbc = m.ReadU32(LightColour);
        _gteRgbc = Gte.Read(6);
        _tinted = true;
        Gte.WriteControl(13, 4096);
        for (int i = 14; i <= 20; i++) Gte.WriteControl(i, 0);
        m.WriteU32(LightColour, (_rgbc & 0xFF000000u) | 255u);
    }

    [PostHook("game", Address = 0x80032588)]
    public static void Restore(CpuContext c, IMemory m) => RestoreLighting(m);

    static void RestoreLighting(IMemory m)
    {
        if (!_tinted) return;
        for (int i = 0; i < Colours.Length; i++) Gte.WriteControl(13 + i, Colours[i]);
        m.WriteU32(LightColour, _rgbc);
        Gte.Write(6, _gteRgbc);
        _tinted = false;
    }
}
