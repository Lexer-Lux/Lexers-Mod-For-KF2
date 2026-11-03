"""Isolated native-loader check. Requires an existing private save fixture.

Example: python verify_native.py --fork-root D:/Programs/LexersVerdite2
    --baseline-data PATH_TO_PRIVATE_FIXTURE --disc PATH_TO_DISC.cue --dotnet PATH_TO_DOTNET

Compiles the original probe source, then starts the installed fork headlessly.
Its save, settings, game build and enabled mod exist only in one temporary folder.
Allow roughly 250 MiB temporary space and up to two minutes. No visible window.
"""
from pathlib import Path
import argparse
import configparser
import hashlib
import json
import os
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fork-root',type=Path,required=True)
    parser.add_argument('--baseline-data',type=Path,required=True)
    parser.add_argument('--disc',type=Path,required=True)
    parser.add_argument('--dotnet',required=True)
    args=parser.parse_args()
    installed=(args.fork_root/'bin').resolve()
    fixture=args.baseline_data.resolve()
    production=Path(os.environ.get('LOCALAPPDATA',''))/'Verdite2'
    if fixture==production.resolve():
        raise ValueError('Use a private copied fixture, not the player data directory')
    names=('interface.ini','settings.json','carda.sav')
    before={n:hashlib.sha256((fixture/n).read_bytes()).digest() for n in names}
    if shutil.disk_usage(tempfile.gettempdir()).free<512*1024*1024:
        raise OSError('The native check needs at least 512 MiB free temporary space')
    source=Path(__file__).resolve().parent.parent
    with tempfile.TemporaryDirectory(prefix='lexeditor-hit-feedback-') as temporary:
        root=Path(temporary)
        data=root/'data'
        data.mkdir()
        shutil.copy2(fixture/'carda.sav',data/'carda.sav')
        settings=json.loads((fixture/'settings.json').read_text(encoding='utf-8-sig'))
        settings.update(CardAPath=str(data/'carda.sav'),CardBEnabled=False)
        (data/'settings.json').write_text(json.dumps(settings,indent=2))
        ini=configparser.ConfigParser(interpolation=None)
        ini.optionxform=str
        ini.read(fixture/'interface.ini',encoding='utf-8-sig')
        ini['RecompOne']['mods.lexer.kf2.hit-feedback.enabled']='True'
        with (data/'interface.ini').open('w',encoding='utf-8') as stream:
            ini.write(stream,space_around_delimiters=False)
        target=data/'mods/hit-feedback'
        target.mkdir(parents=True)
        for name in ('HitFeedback.cs','mod.json'):
            shutil.copy2(source/name,target/name)
        shutil.copy2(source/'tests/Probe.cs.txt',root/'Program.cs')
        project=ET.Element('Project',Sdk='Microsoft.NET.Sdk')
        properties=ET.SubElement(project,'PropertyGroup')
        for name,value in {'OutputType':'Exe','TargetFramework':'net10.0','ImplicitUsings':'enable',
                           'Nullable':'enable','EnableDefaultCompileItems':'false'}.items():
            ET.SubElement(properties,name).text=value
        items=ET.SubElement(project,'ItemGroup')
        ET.SubElement(items,'Compile',Include='Program.cs')
        reference=ET.SubElement(items,'Reference',Include='RecompOne.Runtime')
        ET.SubElement(reference,'HintPath').text=str(installed/'RecompOne.Runtime.dll')
        ET.ElementTree(project).write(root/'HitFeedbackProbe.csproj',encoding='utf-8')
        env=os.environ.copy()
        env.update(VERDITE2_DATA=str(data),VERDITE2_UPDATE_CHECK='0',KF2_HEADLESS='1',KF2_AUTOSTART='1',
                   KF2_ICON='off',DOTNET_PROCESSOR_COUNT='2',KF2_PREJIT_THREADS='1')
        subprocess.run([args.dotnet,'build',str(root/'HitFeedbackProbe.csproj'),'-c','Release',
                        '--nologo','-v','quiet','-m:1'],env=env,check=True,timeout=60,
                       creationflags=subprocess.CREATE_NO_WINDOW)
        result=subprocess.run([args.dotnet,str(root/'bin/Release/net10.0/HitFeedbackProbe.dll'),
                               str(installed),str(args.disc.resolve())],env=env,cwd=data,
                              stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=100,
                              creationflags=subprocess.CREATE_NO_WINDOW)
        print('\n'.join(result.stdout.splitlines()[-30:]))
        result.check_returncode()
    after={n:hashlib.sha256((fixture/n).read_bytes()).digest() for n in names}
    assert before==after,'The private baseline fixture changed'


if __name__=='__main__':
    main()
