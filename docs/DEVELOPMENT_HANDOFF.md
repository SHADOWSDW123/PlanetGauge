# PlanetGauge 개발 핸드오프

최종 갱신: 2026-08-26

이 문서는 새 개발 채팅의 단일 진입점이다. 먼저 이 파일과 실제 Git 상태를 대조한 뒤 사용자가 요청한 작업부터 진행한다.

## 현재 상태

| 항목 | 값 |
|---|---|
| 작업 브랜치 | `codex/0.1.5c` |
| 생산 기준선 | `cdfa113`의 0.1.5A 코드 |
| 마지막 제품 코드 변경 | `63492ea` |
| 타깃 | .NET Framework 4.8 / C# 7.3 |
| 현재 상태 | 0.1.5C 선별 리팩터링 반영 완료 |

## 새 채팅에서 먼저 할 일

```powershell
git status --short --branch
git log --oneline --decorate -8
```

1. 브랜치와 dirty 상태를 확인한다.
2. 사용자의 현재 요청과 관련된 파일만 읽는다.
3. 기존 변경과 겹치는 부분을 확인한 뒤 최소 범위로 수정한다.
4. 관련 없는 리팩터링이나 구조 확장은 추가하지 않는다.

## 0.1.5C에 적용된 변경

- LevelEvent 대형 파일을 책임별 6개 파일로 분리
- 임시 `noFail`의 실제 원값 복원
- `CheckPostHoldFail(ulong?)`, `ParseEnum<T>(string,T)` reflection signature 강화
- pause Restart/ResetCustomLevel을 포함한 통합 세션 초기화
- custom effect 생성 전 floor·timing 입력 검증과 부분 생성 롤백
- 살아 있는 `0.x` 체력의 정수 HUD를 `1`로 표시
- optional 에디터 아이콘 실패 격리
- `Die()` 예외 후 중복 사망 진입 방지
- 실제 시각 설정 변경에만 style revision 갱신
- 사용되지 않는 warning command 필드 제거

세부 변경 이력은 [updates.md](updates.md)의 `0.1.5C` 절을 참조한다.

## 가져오지 않은 0.1.5B 변경

다음 구조는 재현 근거보다 회귀 표면이 커서 C에 넣지 않았다.

- `scrMarginTracker.AddHit` Harmony patch와 판정 observer
- sequence 기반 Die token
- `OptionalValue<T>` command 계층
- 임의의 warning 상한 및 측정 없는 미세 최적화
- `PlanetGauge.Tests.exe`
- `GaugeStateKernel`

실제 판정 불일치가 확인되지 않는 한 판정 source of truth를 교체하지 않는다. 명확한 필요성이 생기기 전에는 `GaugeStateKernel`을 도입하지 않는다.

## 수정 시 지켜야 할 경계

- 구조 이동과 동작 변경은 검토하기 쉽도록 분리한다.
- Harmony target을 바꾸기 전에 설치된 `Assembly-CSharp.dll`의 선언 타입과 정확한 overload를 확인한다.
- 게임 설치 폴더에 파일을 자동 배포하거나 게임을 자동 실행하지 않는다.
- 기존 `SetPlanetGauge` 이름, 숫자 ID `20551`, JSON key, O/X 계약을 유지한다.
- 다른 모드의 Harmony patch나 registry 항목을 제거하지 않는다.

## 관련 파일

| 영역 | 파일 |
|---|---|
| 활성화·reflection·임시 no-fail | `PlanetGauge/Main.cs` |
| 판정·Restart·사망 patch | `PlanetGauge/Patches.cs` |
| 게이지 상태 | `PlanetGauge/GaugeRuntime.cs` |
| HUD | `PlanetGauge/MainGaugeHud.cs` |
| event 계약·registry | `PlanetGauge/PlanetGaugeEventTypes.cs`, `PlanetGauge/PlanetGaugeLevelEventRegistry.cs` |
| event 직렬화·에디터 | `PlanetGauge/PlanetGaugeLevelEventSerializationPatches.cs`, `PlanetGauge/PlanetGaugeLevelEventEditorPatches.cs` |
| event effect·적용 | `PlanetGauge/PlanetGaugeLevelEventEffects.cs`, `PlanetGauge/PlanetGaugeLevelEventApplyPatch.cs` |

기능 계약은 [logics.md](logics.md), 전체 위험 분석은 [needfix.md](needfix.md), 버전 이력은 [updates.md](updates.md)를 참조한다.
