# AI 활용 내역

> 공모전 규정(AI 활용 비율, 전체 제작 위임 허용 여부, 증빙 방식)은 아직 확인하지 않았습니다.
> 이 문서는 실제로 있었던 작업만 적었습니다. 제출 전에 공식 모집요강의 요구 형식에 맞춰 보완하세요.

## 1. 사용한 AI 도구

| 항목 | 내용 | 확인 상태 |
|---|---|---|
| 도구 | Claude Code (Claude 데스크톱 앱의 Code 탭) | 확인됨 |
| 모델 | 작업 환경이 "Claude Opus 5.5 (claude-opus-5-5)"로 표시함 | **사용자 확인 필요** (앱 설정 화면에서 실제 모델명을 확인해 적어 주세요) |
| Unity 연동 | Unity CLI 1.0.0-beta.10 + 프로젝트의 com.unity.pipeline 0.10.0-exp.1 패키지를 통해 열려 있는 Unity Editor에 명령을 보냄 | 확인됨 |
| 작업 날짜 | 2026-11-04 | 확인됨 |

## 2. AI가 한 일

사용자가 한 번에 전달한 상세 기획서(화면 흐름, 미션·제품 수치, 점수식, 연출, 검증 항목, 문서 목록)를 바탕으로 다음을 수행했습니다.

1. 프로젝트 환경 확인: Unity 버전, 패키지, 설치된 빌드 모듈, 연결된 Unity 제어 도구, 사용 가능한 한글 글꼴과 라이선스
2. 코드 작성 (`Assets/ProcurementHero/Scripts/`)
   - Core: 점수·성공 판정·진행·피드백 문구 (Unity 없이 테스트 가능한 순수 C#)
   - Runtime: 데이터 ScriptableObject, 화면 흐름, 제품 카드 UI, 도형 그래픽과 연출, 합성 효과음, 저장
   - Editor: Setup Game / Validate Game / Build Windows 메뉴
   - Tests: EditMode 테스트 20개
3. 기획서의 미션 수치를 데이터 에셋 초기값으로 옮김. 주민 요청·반응 문구와 제품 한 줄 설명은 AI가 작성
4. Unity Editor에서 직접 실행: Setup Game(씬·데이터·폰트 에셋 생성), Validate Game, EditMode 테스트, 플레이 모드에서 흐름 검증과 화면 캡처, Build Windows
5. 발견한 오류 수정 (아래 3절)
6. 문서 작성 (`Docs/`)

## 3. 제작 중 수정 과정 (실제로 있었던 일)

| 발견한 문제 | 수정 |
|---|---|
| `Screen` 열거형 이름이 UnityEngine.Screen과 겹칠 위험 | `GameScreen`으로 변경 |
| 새 스크립트(FocusRing.cs)가 컴파일 목록에 들어가지 않음 | 파일을 다시 만들어 재등록 |
| TMP API 차이(`fontWeightTable` 읽기 전용, `HasCharacters` 인자 형식) | 이 Unity 버전의 실제 API를 리플렉션으로 확인해 맞춤 |
| TMP Essential Resources 가져오기가 비동기로 끝나 폰트 에셋 생성이 실패 | 가져오기 완료 후 Setup Game이 자동으로 이어서 실행되도록 변경 |
| 글꼴에 '♬' 글자가 없음 (Validate Game이 발견) | '♪'만 사용 |
| 타이틀 메뉴가 마을 그림과 겹침, '강당' 글자가 문에 가려짐, 시설 밖으로 배송 차량이 비침, 설치 전 장치 일부가 보임 | 배치 조정, 마스크 위치 변경, 투명도 처리 (화면 캡처로 확인) |
| 결과 공개 후에도 헤더에 "배송 차량이 출발했어요"가 남음 | "배송 완료 · 결과가 확정됐어요"로 바꿈 |
| 보조 버튼이 흰 배경과 구분되지 않음 | 보조 버튼 색을 옅은 베이지로 변경 |

## 3-1. v2 작업(2026-11-06)에서 있었던 일

- 사용자가 준 기획서와 프롬프트 원문은 `Docs/v2/`에 보관했습니다.
- 미션 1 수치는 기획서 그대로, 미션 2·3의 비용·효과·배치는 AI가 정한 개발 초안입니다(`draftNumbers`).
- 작업 중 iCloud 동기화 폴더 때문에 Unity 패키지 캐시가 깨져 에디터가 한 번 충돌했고, 생성 폴더(Library 일부)를 지우고 에디터를 다시 열어 복구했습니다.
- 에디터 키 시뮬레이션의 한계 때문에 현장 단축키를 직전 프레임 비교 방식으로 감지하도록 바꿨습니다.
- `FieldDatabase` 클래스를 다른 이름의 파일에 두어 에셋이 다시 불러와지지 않던 문제를 테스트가 찾아 별도 파일로 옮겼습니다.
- field-polish(같은 날, `feature/field-polish`): 미션 2·3 배치·물품(이동형 발전기 추가)·메모·도착 문구를 AI가 다시 정했습니다(여전히 개발 초안). 이미지 생성 도구는 연결되어 있지 않아 모든 그림을 코드 도형으로 만들었고, 외부 이미지를 쓰지 않았습니다. 플레이 검증 중 도착 중 일시정지 무시·결과 수첩 깜빡임·말풍선 잘림을 찾아 고쳤습니다.

## 4. 사람이 검수해야 할 항목

- [ ] 실제 Windows PC에서 처음부터 엔딩까지 플레이 (Mac에서는 Windows 실행을 확인하지 못함)
- [ ] 모든 화면 문구의 맞춤법·어투, 주민 반응 문구가 의도에 맞는지
- [ ] 게임 속 설명이 실제 제도처럼 오해될 표현이 없는지 (특히 "혁신형", "시범구매" 관련)
- [ ] 정책 참고자료 링크의 내용·날짜 확인
- [ ] Pretendard 글꼴을 공식 릴리스 파일로 교체하고 OFL.txt를 공식 LICENSE로 교체
- [ ] 공모전의 AI 활용 비율·위임 범위 규정 확인 후 이 문서를 그 형식에 맞게 보완
- [ ] 시연 영상 촬영과 편집 (아직 촬영하지 않음)

## 5. 프롬프트와 수정 과정 기록 방법

- 이 대화의 전체 기록은 Claude 데스크톱 앱의 세션 기록에 남아 있습니다. 제출 증빙이 필요하면 앱에서 대화를 내보내거나 화면을 캡처해 보관하세요. (이 문서는 대화를 대신하지 않습니다.)
- 이후 수정 작업을 할 때마다 아래 표에 한 줄씩 추가하는 방식을 권장합니다.

| 날짜 | 요청(프롬프트 요지) | AI가 바꾼 파일 | 사람이 직접 바꾼 것 |
|---|---|---|---|
| 2026-11-04 | Unity CLI 연동 여부 확인 | 없음 | 없음 |
| 2026-11-04 | 상세 기획서 전달 → 게임 전체 구현·검증·빌드·문서 | `Assets/ProcurementHero/**`, `Docs/**`, ProjectSettings(제품명·해상도·빌드 씬) | (기록해 주세요) |
| 2026-11-04 | 검토에서 나온 4가지 문제 수정 요청(튜토리얼 키보드 선택, 같은 엔딩의 새 기록 판정, 기록 초기화 후 타이틀 표시, 빌드 실패 시 이전 결과 삭제) | `GameManager.Mission.cs`, `SaveService.cs`, `GameManager.Screens.cs`, `GameManager.Overlays.cs`, `WindowsBuilder.cs`, 새 파일 `BuildPublisher.cs`, 새 테스트 `SaveServiceTests.cs` · `BuildPublisherTests.cs`, `Docs/README.md` | (기록해 주세요) |
| 2026-11-06 | 기획서 2.0(현장 수첩)과 제작 프롬프트 v2 전달 → 새 씬·규칙·데이터·테스트 구현, 단계별 커밋, 작업 브랜치 푸시 시도 | `Scripts/Core/Field/**`, `Scripts/Runtime/Field/**`, `Scripts/Editor/Field*.cs`·`DefaultFieldData.cs`, `Data/Field/**`, `Scenes/FieldNotebook.unity`, 테스트 2개, 기존 `AudioService`·`GameSetup`·`GameValidator`·`WindowsBuilder` 일부, `Docs/**` | (기록해 주세요) |
| 2026-11-06 | 현장 수첩 개선(탐색 의미, 미션 2·3 퍼즐 재설계, 수첩·페이지·도착·캐릭터·주민·설치 연출, 안정성) 구현·검증·커밋·푸시 시도 | `Scripts/Runtime/Field/**`(새 파일 `FieldBook.cs`), `Scripts/Core/Field/FieldRules.cs`, `ShapeSprites.cs`, `Scripts/Editor/DefaultFieldData.cs`·`FieldSetup.cs`·`FieldValidator.cs`, `Data/Field/*.asset`, 테스트 2개, `Docs/**` | (기록해 주세요) |
| 2026-11-06 | 2차 수정: 그늘막·주민 위치 불일치, 상황에 맞는 대사, 결과 반영 엔딩, 설치 건너뛰기 임시 오브젝트 정리, 미션 2·3 배치 균형 | `FieldMissionData.cs`(주민 반응·새 물품 그림), `FieldWorld.cs`, `FieldArt.cs`, `FieldGame.cs`, `FieldGame.Screens.cs`, `DefaultFieldData.cs`, `FieldValidator.cs`, `Data/Field/*.asset`, `FieldDataTests.cs`, `Docs/**` | (기록해 주세요) |
| 2026-11-06 | 3차: 실제 입력 검증(원인은 자동 조작 환경), 보건소 냉방 단서, 엔딩 글 줄이기·주민 중심 연출, 진행 자동 저장·이어하기 | `FieldSave.cs`(새 파일), `FieldGame.cs`, `FieldGame.Notebook.cs`, `FieldGame.Screens.cs`, `FieldWorld.cs`, `DefaultFieldData.cs`, `Data/Field/FieldMission2_Clinic.asset`, `FieldDataTests.cs`, `FieldSaveTests.cs`(새 파일), `Docs/**` | (기록해 주세요) |
