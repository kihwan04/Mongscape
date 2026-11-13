"""Current V13 plan; older inputs may be explicitly restored from Git into ignored QA storage."""
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def screen_plan_path(version):
    if version not in ('V10','V11','V12','V13'):
        raise ValueError('Unsupported screen-plan version: '+str(version))
    folder=ROOT/'docs' if version=='V13' else ROOT/'Builds/QA/PlanningHistory'
    return folder/f'무한옥_화면별_게임기획서_{version}.docx'
