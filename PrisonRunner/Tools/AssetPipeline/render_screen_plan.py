"""Packaged DOCX renderer with explicit isolated Windows engine and bundled Poppler."""
import importlib.util,sys
import argparse
from pathlib import Path
from functools import partial
from screen_plan_paths import screen_plan_path
root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--version',default='V13',choices=['V10','V11','V12','V13'])
parser.add_argument('--qa-subdir',default='final',choices=['','final'])
args=parser.parse_args()
skill=Path('C:/Users/USER/.codex/plugins/cache/openai-primary-runtime/documents/26.1022.11436/skills/documents/render_docx.py')
spec=importlib.util.spec_from_file_location('packaged_render_docx',skill)
renderer=importlib.util.module_from_spec(spec);spec.loader.exec_module(renderer)
engine=root/'Tools/DocumentRuntime/LibreOffice/program/soffice.exe'
assert engine.exists(),engine
renderer._resolve_soffice=lambda:str(engine)
poppler='C:/Users/USER/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin'
renderer.convert_from_path=partial(renderer.convert_from_path,poppler_path=poppler)
renderer.pdfinfo_from_path=partial(renderer.pdfinfo_from_path,poppler_path=poppler)
output_dir=root/f'Builds/QA/Documents/{args.version}'
if args.qa_subdir:output_dir=output_dir/args.qa_subdir
sys.argv=[str(skill),str(screen_plan_path(args.version)),'--output_dir',str(output_dir),'--emit_pdf']
renderer.main()
