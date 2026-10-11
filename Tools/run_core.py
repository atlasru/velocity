#!/usr/bin/env python3
"""Run the real C# harness with Roslyn when sandboxed process metadata blocks dotnet CLI."""
from pathlib import Path
import json, os, subprocess, sys

root=Path(__file__).resolve().parents[1]
sdk=Path(os.environ.get('VELOCITY_DOTNET_ROOT','/tmp/velocity-dotnet'))
compiler=sorted((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
references=sorted((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
runtime=sorted((sdk/'shared/Microsoft.NETCore.App').iterdir())[-1].name
artifacts=root/'artifacts'; artifacts.mkdir(exist_ok=True)
files=sorted((root/'Assets/Velocity/Core').rglob('*.cs'))+sorted((root/'Tests/CoreHarness').glob('*.cs'))
response=artifacts/'csc.rsp'
response.write_text('\n'.join(['-nologo','-warnaserror','-target:exe','-langversion:9','-out:'+str(artifacts/'CoreHarness.dll')]+['-r:'+str(p) for p in references.glob('*.dll')]+[str(p) for p in files]))
subprocess.run([str(sdk/'dotnet'),str(compiler),'@'+str(response)],cwd=root,check=True)
(artifacts/'CoreHarness.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime}}}))
sys.exit(subprocess.run([str(sdk/'dotnet'),str(artifacts/'CoreHarness.dll')],cwd=root).returncode)
