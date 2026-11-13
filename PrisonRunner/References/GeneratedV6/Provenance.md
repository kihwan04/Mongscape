# 실제 생성 재질 · V6

- 도구: 내장 image_gen (CLI/API 대체 사용 안 함).
- 결과: Unity/Assets/External/Staging/GeneratedV6/HandPaintedAtlas.png. 기존 이미지를 수정/분할하지 않고 atlas 전체를 셰이더에서 좌표로 샘플링한다.
- 생성한 이미지의 원본 사본은 Codex generated_images에 남긴다. 프로젝트가 참조하는 파일은 프로젝트 안에 복사했다.
- 암석/목재/콘크리트/사육장 포장석 네 타일을 검수했다. 반복 경계가 완벽하게 이어진다고 가정하지 않으며 실제 맵에서 투영 크기와 반복감을 추가 확인한다.

## 사용한 프롬프트

Use case: stylized-concept. Asset type: production albedo texture atlas for a Unity stylized cartoon 3D mine escape runner game. Create one square high resolution 2048x2048 image consisting EXACTLY of four equal square material tiles in a 2x2 grid with NO margins, NO borders, NO text, NO labels, NO objects, NO perspective. Upper-left quadrant: gray blue fractured basalt rock surface, hand-painted broad angular planes, small restrained cracks, medium value, readable cartoon stone. Upper-right: warm honey brown old timber plank surface with longitudinal wood grain, knots, chipped paint, broad painted strokes. Lower-left: desaturated blue gray concrete slabs with subtle bevel-edge highlights, fine grunge and sparse cracks, industrial prison floor material. Lower-right: muted green and gray cobblestone walkway slabs with occasional moss at joints, readable broad stylized painted shapes. Each quadrant must be an orthographic flat texture swatch, designed to tile seamlessly when that quadrant is extracted and repeated. Lighting is neutral diffuse ALBEDO only, no cast shadows, no dramatic baked lighting, no vignette, no glossy glare. Cohesive polished stylized mobile-game material language with clean large forms and subtle detail. Do not depict a screenshot, character or map scene. These actual texture tiles will be sampled on real 3D meshes.
