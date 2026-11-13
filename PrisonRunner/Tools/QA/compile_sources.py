from pathlib import Path
import re,subprocess,json
unity=Path(__file__).resolve().parents[2]/'Unity'
qa=unity.parent/'Builds/QA/Compile';qa.mkdir(parents=True,exist_ok=True)
rspdir=unity/'Library/Bee/artifacts/1900b0aE.dag'
csc=Path('C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Data/DotNetSdkRoslyn/csc.dll')
dotnet=csc.parent.parent/'NetCoreRuntime/dotnet.exe'
folders={'Muhanok.LegacyCore':'Core','Muhanok.Domain':'Domain','Muhanok.Application':'Application','Muhanok.Infrastructure':'Infrastructure','Muhanok.Presentation':'Presentation','Muhanok.Content':'Content','Muhanok.Bootstrap':'Bootstrap','Muhanok.PoseInference':'PoseInference','Muhanok.EditModeTests':'Tests/EditMode','Muhanok.PlayModeTests':'Tests/PlayMode','Assembly-CSharp-Editor':'Editor'}
results=[]
for name,folder in folders.items():
 source=rspdir/(name+'.rsp')
 if not source.exists():continue
 text=source.read_text(encoding='utf-8-sig')
 text=re.sub(r'^"Assets/.*?\.cs"\s*$', '',text,flags=re.M)
 text=re.sub(r'^-out:.*$',f'-out:"{qa.as_posix()}/{name}.dll"',text,flags=re.M)
 text=re.sub(r'^-refout:.*$',f'-refout:"{qa.as_posix()}/{name}.ref.dll"',text,flags=re.M)
 text=re.sub(r'^[-/]additionalfile:.*$', '',text,flags=re.M)
 text=re.sub(r'Library/Bee/artifacts/1900b0aE.dag/([^"\n]+\.ref.dll)',lambda m:f'{qa.as_posix()}/{m[1]}' if (qa/m[1]).exists() else m[0],text)
 if name=='Muhanok.Infrastructure':text+='\n-r:"C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Data/Managed/UnityEngine/UnityEngine.UnityWebRequestModule.dll"\n'
 text+='\n'+'\n'.join('"'+p.relative_to(unity).as_posix()+'"' for p in sorted((unity/'Assets/Game'/folder).rglob('*.cs')))+'\n'
 target=qa/(name+'.rsp');target.write_text(text,encoding='utf-8')
 run=subprocess.run([str(dotnet),str(csc),'@'+str(target)],cwd=unity,capture_output=True,text=True)
 (qa/(name+'.log')).write_text(run.stdout+run.stderr,encoding='utf-8')
 print(name, 'PASS' if run.returncode==0 else 'FAIL',run.stdout[-5000:],flush=True)
 results.append(dict(assembly=name,success=run.returncode==0))
 if run.returncode:break
(qa/'results.json').write_text(json.dumps(results,indent=2))
raise SystemExit(0 if all(v['success'] for v in results) else 1)
