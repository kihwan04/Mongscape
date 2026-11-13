"""Verify the retained V13 package without old screenshots or V10-V12 inputs."""
from pathlib import Path
import argparse, hashlib, json, posixpath, re, zipfile
from xml.etree import ElementTree as ET
from screen_plan_paths import screen_plan_path

root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--version',default='V13',choices=['V13'])
parser.add_argument('--rendered-check',action='store_true',help='Also verify a freshly rendered 29-page PDF/PNG set.')
args=parser.parse_args()
path=screen_plan_path(args.version)
ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main','wp':'http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing','a':'http://schemas.openxmlformats.org/drawingml/2006/main','r':'http://schemas.openxmlformats.org/officeDocument/2006/relationships'}
with zipfile.ZipFile(path) as package:
    assert package.testzip() is None,'DOCX CRC failure'
    document=ET.fromstring(package.read('word/document.xml'))
    tables=document.findall('w:body/w:tbl',ns)
    assert [len(table.findall('w:tr',ns)) for table in tables]==[9,11,6,6]
    figures=document.findall('.//wp:inline',ns);assert len(figures)==30
    assert not document.findall('.//w:footerReference',ns)
    for name in package.namelist():
        if name.startswith('word/footer') and name.endswith('.xml'):
            footer=ET.fromstring(package.read(name))
            assert not any(footer.findall('.//w:'+kind,ns) for kind in ['t','fldSimple','instrText','drawing','tbl']),name
    relationships=ET.fromstring(package.read('word/_rels/document.xml.rels'))
    images={item.attrib['Id']:item for item in relationships if item.attrib.get('Type','').endswith('/image')}
    for drawing in document.findall('.//a:blip',ns):
        relation=images[drawing.attrib['{'+ns['r']+'}embed']]
        assert relation.attrib.get('TargetMode')!='External','An embedded figure must not depend on an external PNG'
        target=posixpath.normpath('word/'+relation.attrib['Target']);assert target in package.namelist(),target
    media={name:hashlib.sha256(package.read(name)).hexdigest() for name in package.namelist() if name.startswith('word/media/')}
    text='\n'.join(''.join(node.itertext()) for node in document.findall('.//w:t',ns))
    assert not re.search(r'\d+(?:\.\d+)?\s*m(?:\b|/s)|\d+(?:\.\d+)?\s*미터',text)
    for phrase in ['맵 길이와 장애물 사이 간격은 아직 미정','거리와 간격은 실제 플레이를 보며 정한다','점프가 우선','V10 촬영본']:assert phrase in text,phrase
report={'version':args.version,'docxSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'embeddedFigures':len(figures),'tables':len(tables),'embeddedMedia':media,'renderedPagesChecked':False}
if args.rendered_check:
    from pypdf import PdfReader
    review=root/'Builds/QA/Documents/V13/final'
    pdf=PdfReader(review/path.with_suffix('.pdf').name);assert len(pdf.pages)==29
    for number,page in enumerate(pdf.pages,1):
        text=page.extract_text();assert len(text)>200 and not re.search(r'무한옥\s+V1[123]\s+·',text)
        assert (review/f'page-{number}.png').is_file(),number
    for number,title in [(22,'맵 순서와 운동 배치'),(23,'플레이 진행에 따른 속도와 안내'),(24,'조작 기준과 플레이테스트')]:assert f'{number} {title}' in pdf.pages[number-1].extract_text()
    report['renderedPagesChecked']=True;report['renderedPages']=29
destination=root/'Builds/QA/Documents/V13/document-audit.json';destination.parent.mkdir(parents=True,exist_ok=True)
destination.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('SCREEN_PLAN_STRUCTURE_PASS: V13 / 30 embedded figures / 4 tables / no external screenshot dependency'+(' / 29 rendered pages' if args.rendered_check else ' / rendering not repeated'))
