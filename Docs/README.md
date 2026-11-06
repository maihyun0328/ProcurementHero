# 조달 히어로: 마을을 바꾸는 선택

공공구매의 가치를 알리기 위한 한국어 2D 창작 게임입니다. 실제 계약 절차·평가 방식·제품 성능을 재현하지 않으며, 조달청 공식 제작물이 아닙니다.

| 버전 | 씬 | 상태 |
|---|---|---|
| **v2 현장 수첩** (2026-11-06) | `Assets/ProcurementHero/Scenes/FieldNotebook.unity` | **현재 시작 씬(Build Settings 0번)**, Windows 빌드 대상 |
| v1 카드 비교 (2026-11-04) | `Assets/ProcurementHero/Scenes/ProcurementHero.unity` | 보존. 빌드 목록에 남아 있지만 빌드에는 포함하지 않음 |

---

## v2 현장 수첩 버전

조달청에서 현장 지원을 나온 담당자(가상 설정)가 되어 마을을 걸어 다니며 문제를 관찰하고, 펼친 수첩에서 설치 장소와 물품에 체크한 뒤 계획을 확정합니다. 확정한 계획대로 물품이 배송·설치되고, 바뀐 현장을 다시 살펴본 뒤 별 평가를 받습니다. 규칙은 `Docs/GAME_DESIGN.md`의 v2 절, 원문 기획서는 `Docs/v2/`.

### 실행

1. Unity Hub에서 이 폴더를 **6000.0.61f1**로 엽니다.
2. `Assets/ProcurementHero/Scenes/FieldNotebook.unity`를 열고 Play.
3. 씬이나 데이터가 없다면 `Tools/Procurement Hero/Setup Game`(폰트·기존 데이터) → `Tools/Procurement Hero/Setup Field Notebook`(새 데이터·씬) 순서로 실행합니다. 이미 있는 미션 에셋은 덮어쓰지 않습니다.

> **주의 — iCloud 동기화 폴더**: 이 저장소는 iCloud Drive와 동기화되는 `Documents` 폴더 안에 있습니다. 2026-11-06 작업 중 Unity의 `Library/PackageCache`가 동기화 도중 비거나 늦게 나타나 패키지 어셈블리가 등록되지 않는 문제(UGUI·Input System 타입을 찾지 못하는 컴파일 오류, 에디터 충돌)가 있었습니다. `Library/PackageManager`, `Library/PackageCache`, `Library/ScriptAssemblies`, `Library/Bee`를 지우고 에디터를 다시 열어 해결했습니다. 같은 문제가 반복되면 프로젝트를 iCloud 동기화 밖(예: `~/Developer`)으로 옮기는 것을 권장합니다.

### Editor 메뉴 (v2)

| 메뉴 | 하는 일 |
|---|---|
| Setup Field Notebook | `Data/Field/`에 FieldDatabase와 미션 3개(없을 때만) 생성, `FieldNotebook.unity`에 카메라·Canvas(1920×1080 기준)·EventSystem(InputSystemUIInputModule)·FieldGame 참조 연결, 빌드 목록에 추가(시작 씬은 바꾸지 않음). 재실행해도 오브젝트가 중복되지 않음 |
| Use Field Notebook As Start Scene | 새 씬을 Build Settings 0번으로. 기존 씬 항목은 남김 |
| Validate Field Notebook | ID·참조·비용, 예산 안 성공 조합과 별 3개 조합 존재, 관찰 지점이 시작 위치에서 걸어서 닿는지(격자 탐색), 시작 위치가 막혔는지 |
| Build Windows | 시작 씬 하나를 임시 폴더에 빌드 → 배포 파일·ZIP 준비 → 성공 시에만 기존 결과와 교체 |

### 조작

| 입력 | 현장 | 수첩 |
|---|---|---|
| WASD · 방향키 | 이동 | 항목 이동 |
| E · Enter · 돋보기 클릭 | 가까운 대상 관찰 | (Enter) 선택 항목 실행 |
| Tab · 수첩 버튼 | 수첩 열기 | 수첩 닫기 |
| Esc | 일시정지 | 계획을 유지하고 수첩 닫기 |
| Space | 설치 연출 건너뛰기 | |

### 실제로 수행한 검증 (2026-11-06, 이 Mac의 Unity 6000.0.61f1)

| 항목 | 방법 | 결과 |
|---|---|---|
| 컴파일 | 에디터 재컴파일 | 오류 0 |
| EditMode 테스트 | TestRunnerApi | **60개 통과** (v1 38 + v2 규칙 15 + v2 데이터 7) |
| 규칙 테스트(Unity 밖) | csc + NUnit로 Core 단독 컴파일·실행 | v2 규칙 15 + v1 규칙 13 통과 |
| 미션 1 27개 조합, 기준 사례 5개, 75 허용·76 차단 | 테스트 | 통과 |
| 체크 교체·취소 비용 누적 없음, 설치 불가 물품, 중복 해결 1회, 확정 스냅샷 독립 | 테스트 | 통과 |
| 미션 2·3 성공·별 3개 조합 존재, 관찰 지점 도달 가능 | Validate Field + 테스트 | 통과 |
| 새 기록 키 분리, 같은 별 재등록은 새 기록 아님, v2 초기화가 v1 점수 보존 | 테스트(메모리 저장소) | 통과 |
| 이동·충돌 | 플레이 모드, 실제 키(D·W·A) | 이동, 건물 아래 가장자리(y=0.5)에서 정확히 멈춤 |
| 대각선 정규화 | 이동 함수에 (1,1) 입력 | 속도 (4.24, 4.24), 크기 6 = 직선과 같음 |
| 근접 관찰 | 실제 E키 | 먼 곳에서는 무시, 가까이에서 1회 기록, 반복해도 중복 없음 |
| Esc 분리 | 실제 Esc | 현장: 일시정지(timeScale 0), 다시 Esc로 해제 / 수첩: 닫기만 / 일시정지 위 설정 창을 Esc 두 번으로 닫으면 timeScale 1 |
| 수첩 유지 | Tab 여러 번 | 계획·선택 지점 유지, 닫으면 키보드 선택 비움, 수첩 중 이동 잠금 |
| 키보드 수첩 조작 | uGUI 이동·제출 이벤트 | 지점 선택·체크·교체·취소·확정까지 키보드만으로 진행 |
| 확정 연타 | 확정 함수 3회 | 첫 번째만 받아들임, 결과 스냅샷 1개 |
| 설치 건너뛰기 | 실제 Space | 3개 모두 설치된 상태로 재확인 단계 |
| 별 0·1·2·3 결과, 재계획, 다음 현장 | 플레이 | 모두 표시, 별 0개에서도 결과·재계획 진행, 재계획 결과가 이전 결과를 대체 |
| 타이틀 → 현장 1·2·3 → 엔딩 | 플레이 | 완주(★3·★2·★3 = 8/9) |
| 한글·해상도 | 화면 캡처 1920×1080, 1366×768 | 네모 글자 없음, 수첩·결과 잘림 없음 |
| Console | 플레이 중 | 오류·예외 없음 |
| Windows 빌드 | Build Windows | **성공**(102초). 빌드에는 FieldNotebook 씬만 포함, 기존 11월 4일 결과를 성공 후 교체 |

키 입력 방식 주의: 에디터의 키 시뮬레이션은 '눌린 상태'만 바꾸고 '이번 프레임에 눌림'은 만들지 않아, 현장 단축키(E·Tab·Esc·Space·Enter)는 직전 프레임 상태와 비교해 직접 감지하도록 구현했습니다(실제 키보드에서도 같은 동작). 시뮬레이터는 동시에 두 키를 누를 수 없어 대각선은 이동 함수로 확인했습니다. 수첩의 방향키·Enter는 uGUI 이벤트로 확인했습니다.

### 확인하지 못한 것 (v2)

- **Windows에서 실행하지 않았습니다.** 빌드 파일만 만들었습니다.
- 실제 마우스 클릭(돋보기 클릭, 멀리서 클릭, UI 위 클릭이 현장으로 가지 않는지): 포인터 시뮬레이션 좌표가 Game 뷰 크기와 맞지 않아 시험하지 못했습니다. 코드로는 UI 위 클릭을 현장 관찰에서 제외합니다.
- 사람이 실제 키보드·마우스로 처음부터 끝까지 플레이한 확인.
- 소리는 볼륨 값만 확인했고 귀로 듣지 않았습니다.
- 첫 플레이 10~15분 분량, 초심자가 설명 없이 다음 행동을 찾는지는 사람 테스트가 필요합니다.
- 미션 2·3 수치는 개발 초안입니다.
- 16:9가 아닌 화면 비율.

---

## v1 카드 비교 버전 (보존)

새봄마을 공공구매 지원팀 신입 담당자가 되어 학교·보건소·대피소의 요청에 맞는 물품 두 가지를 고르는 카드 선택형 전략 퍼즐입니다.
아래 내용은 2026-11-04 기준이며, 빌드·시작 씬 관련 설명은 v2로 바뀌었습니다(위 표 참고).

## 1. 환경

| 항목 | 값 |
|---|---|
| Unity | **6000.0.61f1** (Apple Silicon Mac에서 작업) |
| 렌더 파이프라인 | URP 17.0.4 (기존 프로젝트 설정 유지) |
| UI | uGUI + TextMeshPro (com.unity.ugui 2.0.0), 코드로 화면 생성 |
| 입력 | Input System 1.14.2 (`InputSystemUIInputModule`), Active Input Handling은 기존 값(Both) 유지 |
| 테스트 | Unity Test Framework 1.6.0, EditMode |
| 빌드 대상 | Windows 64비트 (Windows Build Support 모듈 설치됨) |

## 2. 폴더

```
Assets/ProcurementHero/
  Scripts/Core/      점수 · 성공 판정 · 진행 · 피드백 (UnityEngine 참조 없는 순수 C#)
  Scripts/Runtime/   데이터 SO, GameManager(화면 흐름), FacilityView(마을 연출), UI, 오디오, 저장
  Scripts/Editor/    Setup Game · Validate Game · Build Windows
  Tests/EditMode/    EditMode 테스트 20개
  Data/              GameDatabase + 미션 3개 (Setup Game이 생성)
  Scenes/            ProcurementHero.unity (Setup Game이 생성, Build Settings 0번)
  Fonts/             Pretendard OTF, TMP 폰트 에셋, OFL.txt
Assets/TextMesh Pro/ TMP Essential Resources (Setup Game이 가져옴)
Builds/              Windows 빌드 폴더와 제출용 ZIP (프로젝트 원본과 분리)
Screenshots/         에디터 플레이 중 캡처한 화면 (검증 기록)
Docs/                문서
```

## 3. 프로젝트 여는 방법

1. Unity Hub에서 이 폴더를 `6000.0.61f1`로 엽니다(다른 버전으로 업그레이드하지 마세요).
2. `Assets/ProcurementHero/Scenes/ProcurementHero.unity`를 엽니다.
3. Play를 누르면 타이틀부터 시작합니다. Hierarchy나 Inspector를 따로 설정할 필요가 없습니다.

## 4. Editor 메뉴 (Tools/Procurement Hero)

| 메뉴 | 하는 일 |
|---|---|
| **Setup Game** | 폴더 · TMP 기본 리소스 · 한글 폰트 에셋 · 미션 데이터 · 씬(카메라, Canvas+CanvasScaler 1920×1080, EventSystem+InputSystemUIInputModule, GameManager 참조) 생성, 시작 씬 등록, 제품명·해상도 설정. 여러 번 실행해도 중복이 생기지 않고, 이미 있는 미션 데이터는 덮어쓰지 않습니다. 저장되지 않은 씬이 있으면 저장 여부를 묻고, 취소하면 아무것도 바꾸지 않습니다. 처음 실행 시 TMP 리소스를 가져오는 동안 잠시 멈췄다가 자동으로 이어서 진행합니다. |
| **Validate Game** | 데이터 누락, 잘못된 비용·성능·배송일, 중복 ID, 씬 참조 누락, 시작 씬 등록, 화면 문구의 폰트 누락 글자, 미션별 성공 가능 조합 존재 여부를 확인합니다. |
| **Build Windows** | 검증 → 임시 작업 폴더(`Builds/Windows/.build_시각/`)에 `BuildPipeline.BuildPlayer`(StandaloneWindows64, 게임 씬만) → `BuildReport` 결과 확인 → 폰트 라이선스·실행 안내 동봉 → 필수 파일 확인 → 임시 ZIP 생성 → 모두 성공했을 때만 기존 실행 폴더·ZIP과 교체(`BuildPublisher.cs`). 어느 단계에서 실패해도 이전 실행 폴더와 `Builds/ProcurementHero_Windows.zip`은 그대로 남거나 되돌려지고, 성공 메시지를 내지 않습니다. 이전 실행 폴더에 빌드 결과가 아닌 파일이 있으면 지우지 않고 `ProcurementHero_previous_시각` 폴더로 보관합니다. Windows 모듈이 없으면 설치 방법을 알려 주고 멈춥니다. |

## 5. 조작

| 입력 | 동작 |
|---|---|
| 마우스 클릭 | 카드 선택/취소, 버튼 |
| 방향키 | 버튼과 카드 사이 이동 (선택된 요소는 남색 테두리) |
| Enter | 선택한 버튼 누르기 |
| Esc | 일시정지 / 열린 창 닫기 |
| Space | 결과 연출 건너뛰기 |

## 6. 빌드 결과 (2026-11-04, 이 Mac에서 실제 수행)

- `Tools/Procurement Hero/Build Windows`와 같은 코드(`WindowsBuilder.Build`)로 빌드: **BuildResult.Succeeded**, 오류 0
- 실행 폴더: `Builds/Windows/ProcurementHero/` (약 100 MB)
  - `ProcurementHero.exe`, `UnityPlayer.dll`, `UnityCrashHandler64.exe`, `ProcurementHero_Data/`, `MonoBleedingEdge/`, `D3D12/`, `LICENSE_Pretendard_OFL.txt`, `README_KO.txt`
  - exe만 따로 옮기면 실행되지 않습니다. **폴더 전체**를 그대로 배포하세요.
- 제출용 ZIP: `Builds/ProcurementHero_Windows.zip` (약 37 MB, 배포 금지 Burst 디버그 폴더 제외)
- Windows 빌드 과정에서 Editor의 활성 빌드 대상이 macOS → Windows(StandaloneWindows64)로 바뀌었습니다. 원래대로 돌리려면 File > Build Profiles에서 macOS를 선택하세요(재임포트가 일어납니다).

## 7. 실제로 수행한 검증

| 항목 | 방법 | 결과 |
|---|---|---|
| 컴파일 | Unity Editor 재컴파일, Console 확인 | 오류 0 (URP 패키지 내부 셰이더 경고만 있음) |
| Setup Game 생성과 재실행 | 두 번 실행 후 씬 루트 확인 | Camera·Canvas·EventSystem·GameManager 각 1개, 데이터 덮어쓰기 없음 |
| Validate Game | 실행 | 오류 0, 화면 문구 433종 글자 모두 폰트에 있음 (처음 실행 때 '♬' 누락을 찾아 수정) |
| EditMode 테스트 20개 | TestRunnerApi | 20 통과 |
| 3×3 조합 27개 전체 계산 | `Scoring.Evaluate` | 미션별 성공 조합 9 / 3 / 1개 (표: `GAME_DESIGN.md`) |
| 미션 2 고효율형 두 개 | 테스트 + 플레이 | 총비용 145 > 140, 구매 버튼 비활성, "예산을 5 넘어 구매할 수 없어요" |
| 미션 3 배송 2일 제품 | 테스트 + 플레이 | 납기계수 0.4, "△ 납기 지연", 부분 해결 연출 |
| 미선택 · 예산 초과 차단 | 플레이 모드 | 버튼 비활성 + 이유 표시 |
| 제품 변경 · 선택 취소 | 플레이 모드 | 총비용이 현재 선택만 반영 (누적 없음) |
| 구매 확정 연타 | 버튼 클릭 2회 + 확정 함수 2회 | 결과 스냅샷 1개, 결과 화면 1회 |
| 예상 점수 = 확정 결과 | 플레이 모드 | 57.9 = 57.9 |
| 연출 일시정지 · 건너뛰기 | 플레이 모드 | 일시정지 3초 동안 진행 멈춤(timeScale 0), 해제 후 1로 복귀, 건너뛰기 즉시 결과 공개, 두 번 눌러도 문제 없음 |
| 재도전 초기화 | 플레이 모드 | 선택 해제, 구매 버튼 비활성, 확정 상태 초기화, 튜토리얼 다시 안 나옴 |
| 최종 점수 중복 가산 방지 | 플레이 모드 | 미션 3 재도전 전 182.0 → 후 196.5 = 세 미션 채택 점수 합계 |
| 엔딩 분기 | 플레이 + 테스트 | 2개 성공 "성장하는 공공구매 해결사", 3개 성공 "모두의 하루를 바꾼 조달 히어로" |
| 새 게임과 최고 기록 초기화 구분 | 플레이 모드 | 처음부터 시작은 확인창 후 진행만 초기화, 최고 기록 초기화는 설정 창의 별도 버튼 |
| 음소거 | 플레이 모드 | 효과음·배경음 볼륨 0 ↔ 0.8 / 0.175 |
| 튜토리얼 | 플레이 모드 | ①~⑤ 진행, 두 물품 선택 시 자동 진행, 구매 시 종료, 점수 영향 없음(53.4) |
| 해상도 | Game 뷰 캡처 1920×1080, 1366×768 | 주요 버튼·글자 잘림 없음 (두 해상도 모두 16:9라 같은 배치가 0.71배로 축소됨) |
| 한글 표시 | 화면 캡처 | 네모 글자 없음 |
| 플레이어 빌드 시작 | macOS 개발 빌드를 1366×768로 12초 실행 | 플레이어 로그 오류 없음 (점검용 빌드는 삭제) |

### 7-1. 2차 수정 검증 (2026-11-04 오후, 실제 수행)

이번 작업에서 고친 4가지 문제와 관련해 실제로 한 확인만 적었습니다.

| 항목 | 방법 | 결과 |
|---|---|---|
| EditMode 테스트 | TestRunnerApi | 38개 통과 (기존 20 + 신규 18: 기록 7, 빌드 교체 11) |
| 컴파일 | Unity Editor 재컴파일 | 오류 0 |
| 같은 엔딩 재등록 | 테스트(메모리 저장소) + 플레이 | 기본형만 산 총점 158.08235294117645를 두 번 등록해도 두 번째는 새 기록 아님. 플레이 중 같은 엔딩을 다시 열면 "★ 새 기록!"이 사라짐 |
| 첫 기록 · 낮은 기록 · 높은 기록 · 성공 수만 증가 | 테스트 | 각각 새 기록 / 아님(기존 유지) / 새 기록 / 점수는 아님·최다 성공 수만 갱신 |
| 튜토리얼 키보드 선택 | 플레이 모드, uGUI 이동·제출 이벤트 | '다음'으로 ③ → 선택이 첫 제품 카드로 이동(같은 Enter로 카드 안 눌림), 카드에서 두 물품을 고르면 ④ 단계에서도 그 카드 선택 유지, '다음'으로 ⑤ → 구매 확정 버튼으로 이동(같은 Enter로 구매 안 됨), Enter로 구매 |
| 튜토리얼 건너뛰기 | 새 플레이 세션 2회 | 미선택 상태에서 건너뛰면 첫 제품 카드로, 두 물품을 고른 뒤 건너뛰면 구매 확정 버튼으로 선택 이동 |
| 키보드만으로 엔딩까지 | 같은 이벤트 방식 | 미션 1~3 선택 · 구매 · 연출 건너뛰기 · 다음 미션 · 엔딩 도달. 결과 화면에서 Enter로 건너뛰어도 '다음 미션'이 같은 입력으로 눌리지 않음 |
| 기록 초기화 후 타이틀 표시 | 타이틀 → 설정 → 초기화 → 취소 / 확정 → 닫기 | 취소하면 기록과 두 문구 유지. 확정하면 타이틀 문구가 즉시 "최고 기록 없음", 진행(196.5점 · 3미션 완료)과 소리 설정 유지, 닫으면 선택이 '소리 설정' 버튼으로 복귀 |
| 빌드 실패 경로 | 시스템 임시 폴더의 가짜 빌드로 테스트 | 빌드 실패 · 빌드 중 예외 · 필수 파일 누락 · ZIP 전후 · 폴더 교체 중 · ZIP 교체 중 실패 모두 이전 실행 폴더와 ZIP 보존(또는 복구), 임시 폴더 정리, 성공 메시지 없음 |
| 실제 Windows 빌드 | Build Windows 코드 | 성공(24초). 기존 실행 폴더와 ZIP이 새 결과로 바뀌고 `.build_` 임시 폴더 남지 않음 |
| 실제 기록 보존 | 작업 전후 PlayerPrefs 확인 | 테스트와 플레이는 메모리 저장소를 써서 사용자의 실제 기록 값이 바뀌지 않음 |
| 콘솔 | 플레이 중 Console 확인 | 오류 · 예외 없음 |

**키 입력 방식에 대한 주의**: 에디터의 키 시뮬레이션(`simulate_key`)은 키보드 장치 상태까지는 바뀌었지만 Game 뷰 포커스 정책 때문에 UI 입력 액션 값이 0으로 남아 화면 조작으로 이어지지 않았습니다. 그래서 InputSystemUIInputModule이 방향키 · Enter를 받았을 때 보내는 것과 같은 uGUI 이동(Move) · 제출(Submit) 이벤트를 EventSystem에 직접 보내 확인했습니다. 실제 물리 키보드로 누른 확인은 아닙니다.

## 8. 확인하지 못한 것과 알려진 제한

- **Windows에서 실행하지 못했습니다.** Mac에서 Windows 빌드 파일만 만들었습니다. Windows PC에서 ZIP을 풀고 처음부터 엔딩까지 플레이해 확인해야 합니다.
- **실제 빌드에서 처음부터 엔딩까지 직접 플레이하지 못했습니다.** 전체 흐름은 Unity Editor 플레이 모드에서 스크립트로 버튼을 눌러 확인했고, 플레이어 빌드는 macOS에서 시작 화면까지만 확인했습니다.
- 사람이 실제 마우스·키보드로 끝까지 플레이한 검증은 아직 없습니다. 키보드 흐름은 uGUI 이동·제출 이벤트로만 확인했습니다(7-1 참고). 실제 키를 눌렀을 때 같은 동작인지는 Game 뷰를 클릭해 포커스를 준 뒤 직접 확인해야 합니다.
- 실패 경로는 가짜 빌드로만 시험했습니다. 실제 Unity 빌드가 실패하는 상황(예: 컴파일 오류)에서 기존 결과가 남는지는 같은 코드 경로를 쓰지만 직접 일으켜 보지는 않았습니다.
- 16:9가 아닌 화면 비율(예: 16:10, 4:3)은 확인하지 않았습니다.
- 소리는 코드로 합성한 간단한 음이며, 실제 스피커 출력은 귀로 확인하지 않았습니다(볼륨 값만 확인).
- Web 빌드는 만들지 않았습니다(WebGL 모듈 미설치).
- Pretendard 파일은 다른 앱에 들어 있던 OFL 파일을 복사했으므로 공식 릴리스로 교체를 권장합니다 (`SOURCES_AND_LICENSES.md`).
- 시연 영상은 촬영하지 않았습니다 (`DEMO_SCRIPT.md`).
- 공모전 규정은 대부분 미확인입니다 (`SUBMISSION_CHECKLIST.md`).

## 9. 프로젝트 설정에서 바꾼 것

- Player Settings: 제품명 `조달 히어로`, 기본 해상도 1920×1080, 창 모드, 창 크기 조절 허용, Run In Background
- Build Settings: `Assets/ProcurementHero/Scenes/ProcurementHero.unity`를 0번에 추가 (기존 `SampleScene` 항목은 그대로 둠, 빌드에는 포함하지 않음)
- `Assets/TextMesh Pro/` 추가
- 기존 `Assets/Scenes/SampleScene.unity`, URP 설정, 입력 설정, 패키지 구성은 바꾸지 않았습니다.
