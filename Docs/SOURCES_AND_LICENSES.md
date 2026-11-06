# 출처와 라이선스

## 1. 정책 참고자료

| 자료 | 링크 | 사용 방식 |
|---|---|---|
| 조달청, 2026-08-18 혁신제품 시범구매 관련 보도자료 | https://pps.go.kr/kor/bbs/view.do?bbsSn=2608180009&key=00634 | 게임 속 소개 화면에 참고자료로 링크만 표시. 본문을 인용하거나 수치를 가져오지 않음 |

- 이 링크의 내용은 제작 과정에서 직접 열어 확인하지 않았습니다. 제출 전에 사람이 내용과 날짜를 확인해야 합니다.
- 게임 속 제품 분류(기본형·고효율형·혁신형), 수치, 점수식은 창작 규칙이며 위 자료나 실제 제도를 재현하지 않습니다.
- 조달청 공식 캐릭터·로고는 사용하지 않았습니다. 조달청이 승인하거나 공식 제작한 게임으로 표시하지 않습니다.

## 2. 글꼴

| 글꼴 | 파일 | 라이선스 | 공식 배포처 |
|---|---|---|---|
| Pretendard Regular / Bold, Version 1.307 (길형진, Kil Hyung-jin) | `Assets/ProcurementHero/Fonts/Pretendard-Regular.otf`, `Pretendard-Bold.otf` | SIL Open Font License 1.1 | https://github.com/orioncactus/pretendard |

- 라이선스 근거: 글꼴 파일 내부 name 테이블에 "This Font Software is licensed under the SIL Open Font License, Version 1.1"과 저작권 "Copyright © 2022 Kil Hyung-jin"이 들어 있는 것을 확인했습니다.
- **파일 출처 주의**: 이 두 파일은 인터넷에서 새로 내려받지 않고, 이 Mac에 이미 설치된 다른 앱(MYBOX.app)의 리소스 폴더에 들어 있던 Pretendard 1.307 파일을 복사했습니다. OFL은 재배포를 허용하지만, **제출 전에 공식 배포처의 릴리스 파일로 교체하는 것을 권장**합니다. 교체 후 Setup Game을 다시 실행하면 폰트 에셋이 그대로 쓰입니다(같은 파일 이름 유지).
- OFL 사본: `Assets/ProcurementHero/Fonts/OFL.txt`. 이 파일의 OFL 본문은 표준 OFL 1.1 전문을 옮긴 것이며, 머리말의 저작권 문구는 글꼴 파일 메타데이터에서 가져왔습니다. 공식 저장소의 LICENSE 파일로 교체해 주세요.
- Windows 빌드 폴더에는 `LICENSE_Pretendard_OFL.txt`로 함께 들어갑니다(OFL 조건: 글꼴을 함께 배포할 때 라이선스 사본 동봉).
- OS 기본 글꼴은 복사하거나 배포하지 않았습니다.
- TextMeshPro 폰트 에셋(`Pretendard SDF.asset`, `Pretendard Bold SDF.asset`)은 동적 SDF 에셋이며, 실행 시 원본 글꼴에서 필요한 글자를 만들어 씁니다.

## 3. 그래픽

| 자산 | 출처 |
|---|---|
| 마을·시설·주민·배송 차량·말풍선·아이콘 | 직접 생성. `ShapeSprites.cs`가 실행 시 사각형·둥근 사각형·원·삼각형·빛 번짐 Sprite를 코드로 만들고, `FacilityView.cs`가 이를 배치·애니메이션함 |
| 외부 이미지 | 사용하지 않음 |

v2 현장 수첩 버전(2026-11-06)의 현장 지형·건물·주민·담당자·물품(가로등·보행등·냉방장치·급수장치·그늘막·비상전원·배수펌프·모래주머니)·수첩·도장 그림도 모두 `FieldArt.cs`·`FieldWorld.cs`에서 기본 도형으로 직접 생성했습니다. field-polish에서 추가한 이동형 발전기, 관찰 전 구역 빗금(`ShapeSprites.Stripes`), 가죽 표지·줄 노트·링 수첩과 페이지 넘김(`FieldBook.cs`), 배송 차·트럭, 담당자·주민 팔다리도 같은 방식입니다. 외부 이미지·유료 에셋·이미지 생성 도구는 쓰지 않았습니다.

## 4. 소리

| 자산 | 출처 |
|---|---|
| 효과음(클릭·선택·구매·도착·성공·부분 해결·실패, v2의 관찰·수첩·체크·체크 취소·확정·설치·도장), 배경음 | 직접 생성. `AudioService.cs`가 실행 시 사인파로 합성 |
| 외부 음원 | 사용하지 않음 |

## 5. 엔진과 패키지

| 항목 | 버전 | 비고 |
|---|---|---|
| Unity Editor | 6000.0.61f1 | Unity 이용 약관에 따름 |
| Universal RP | 17.0.4 | 기존 프로젝트 설정 유지 |
| uGUI + TextMeshPro (com.unity.ugui) | 2.0.0 | TMP Essential Resources를 `Assets/TextMesh Pro`로 가져옴 (Unity 패키지 리소스) |
| Input System | 1.14.2 | UI 입력은 InputSystemUIInputModule |
| Test Framework | 1.6.0 | EditMode 테스트 |
| 기타 | `Packages/manifest.json` 참고 | 기존 2D 템플릿 구성 그대로 |

`Assets/TextMesh Pro/` 안의 LiberationSans 등 TMP 기본 리소스는 Unity 패키지에 포함된 리소스이며 게임 화면에는 쓰지 않습니다.

## 6. 직접 만든 것과 아닌 것 요약

- 직접 생성: 게임 코드, 기획 데이터, 모든 그래픽, 모든 소리, 문서
- 외부 자산: Pretendard 글꼴(OFL 1.1), Unity 엔진과 패키지
- 제작에 AI 도구를 사용했습니다. 범위는 `AI_USAGE.md`를 참고하세요.
