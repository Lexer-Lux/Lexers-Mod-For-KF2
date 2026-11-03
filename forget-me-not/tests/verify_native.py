"""Focused silent private-memory checks; no game launcher, disc, save or audio.

Requires an existing private compiled game SDK directory and .NET10 SDK.
Build output and settings exist only in a bounded temporary directory.
"""
from pathlib import Path
import argparse
import json
import os
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sdk',type=Path,required=True)
    parser.add_argument('--dotnet',required=True)
    parser.add_argument('--runtime',type=Path,required=True)
    args=parser.parse_args()
    sdk=args.sdk.resolve()
    runtime=args.runtime.resolve()
    source=Path(__file__).resolve().parent.parent
    if shutil.disk_usage(tempfile.gettempdir()).free<256*1024*1024:
        raise OSError('At least256MiB free temporary space is required')
    with tempfile.TemporaryDirectory(prefix='kf2-forget-me-not-') as temporary:
        root=Path(temporary)
        project=ET.Element('Project',Sdk='Microsoft.NET.Sdk')
        properties=ET.SubElement(project,'PropertyGroup')
        for key,value in {'OutputType':'Exe','TargetFramework':'net10.0','StartupObject':'Probe','ImplicitUsings':'enable','Nullable':'enable','EnableDefaultCompileItems':'false','AssemblySearchPaths':'{HintPathFromItem};{RawFileName}'}.items():
            ET.SubElement(properties,key).text=value
        items=ET.SubElement(project,'ItemGroup')
        ET.SubElement(properties,'DisableImplicitFrameworkReferences').text='true'
        ET.SubElement(properties,'NoStdLib').text='true'
        ET.SubElement(properties,'UseAppHost').text='false'
        ET.SubElement(properties,'EnableNETAnalyzers').text='false'
        for fixture in source.joinpath('tests').glob('*.cs.txt'):
            ET.SubElement(items,'Compile',Include=str(fixture),Link=fixture.name[:-4])
        for path in source.glob('*.cs'):
            ET.SubElement(items,'Compile',Include=str(path))
        for name in ('KingsField2','RecompOne.Runtime','ImGui.NET'):
            reference=ET.SubElement(items,'Reference',Include=name)
            ET.SubElement(reference,'HintPath').text=str((sdk if name=='KingsField2' else runtime)/(name+'.dll'))
        corelib=sorted((Path(args.dotnet).resolve().parent/'shared/Microsoft.NETCore.App').glob('10.*/System.Private.CoreLib.dll'))[-1]
        for library in corelib.parent.glob('*.dll'):
            if '.Native.' in library.name:
                continue
            if not library.name.startswith(('System.','Microsoft.')) and library.stem not in ('mscorlib','netstandard','WindowsBase'):
                continue
            reference=ET.SubElement(items,'Reference',Include=library.stem)
            ET.SubElement(reference,'HintPath').text=str(library)
            ET.SubElement(reference,'Private').text='false'
        native=runtime/'runtimes/win-x64/native/cimgui.dll'
        if not native.exists(): native=runtime/'cimgui.dll'
        ET.SubElement(items,'None',Include=str(native),Link='cimgui.dll',CopyToOutputDirectory='PreserveNewest')
        ET.ElementTree(project).write(root/'Check.csproj',encoding='utf-8')
        env=os.environ.copy()
        env.update(DOTNET_PROCESSOR_COUNT='1',KF2_PREJIT_THREADS='1',VERDITE2_DATA=str(root),VERDITE2_UPDATE_CHECK='0')
        flags={'creationflags':subprocess.CREATE_NO_WINDOW} if os.name=='nt' else {}
        build=subprocess.run([args.dotnet,'build',str(root/'Check.csproj'),'-c','Release','-m:1','/p:UseSharedCompilation=false','-v','quiet'],env=env,cwd=root,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=60,**flags)
        print(build.stdout)
        build.check_returncode()
        (root/'bin/Release/net10.0/Check.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net10.0','framework':{'name':'Microsoft.NETCore.App','version':corelib.parent.name}}}))
        result=subprocess.run([args.dotnet,str(root/'bin/Release/net10.0/Check.dll'),str(runtime)],env=env,cwd=root,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=20,**flags)
        print(result.stdout)
        result.check_returncode()

if __name__=='__main__':
    main()
