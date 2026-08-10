# PlanetGauge 개발 인수인계

최종 갱신: 2026-08-10  
대상 게임: A Dance of Fire and Ice(얼불춤)  
모드 로더: Unity Mod Manager + Harmony

이 문서는 저장소 루트 기준으로 작성한다. PC별 드라이브 문자, 사용자 폴더, 게임 설치 절대 경로는 기록하지 않는다.

## 1. 현재 상태

| 항목 | 현재 값 |
|---|---|
| 브랜치 | `main` |
| 커밋된 HEAD | `6d7cc69` (`불필요한 백엔드 기능 제거`) |
| 원격 기준 | `origin/main`과 같은 커밋 |
| 태그 | `beta` → `2ade381`, `V0.0.7` → `e191923` |
| 모드 버전 | 미커밋 변경 기준 `0.0.8` |
| 타깃 | .NET Framework 4.8 / C# 7.3 |
| 최근 빌드 | Release, 경고 0개, 오류 0개 |
| 자동 테스트 | 없음 |

2026-08-10 기준으로 추적 파일 8개에 아직 커밋·스테이징되지 않은 정리 변경이 있다. 다음 작업을 시작할 때 반드시 먼저 확인한다.

```powershell
git status --short --branch
git diff
```

## 2. 사실 확인 우선순위

1. 현재 체크아웃한 소스와 Git 상태
2. 해당 PC에 현재 설치된 게임 DLL
3. 실제 게임 플레이 재현 결과
4. 보조 분석 문서와 과거 커밋
5. 이 핸드오프에 적힌 과거 관찰

게임 업데이트 후에는 과거에 복사한 DLL이나 해시를 호환성 근거로 사용하지 않는다. 현재 모드도 DLL 해시를 검사하지 않고 필수 API 존재 여부를 확인한다.

## 3. 모드 목적과 적용 범위

PlanetGauge는 레벨 에디터의 1인 테스트 플레이에 체력형 게이지를 추가한다.

- 실패 방지 버튼 위의 작은 게이지를 눌러 켜고 끈다.
- 새 에디터 세션에서는 기본 OFF다.
- 에디터의 1인 실제 플레이에서만 판정과 실패 흐름을 변경한다.
- 일반 플레이와 협동 플레이에는 적용하지 않는다.
- 자동 플레이, 자동 타일, `midspinInfiniteMargin`, `noFailInfiniteMargin` 구간은 건너뛴다.
- 판정 오차 미터 위에 메인 체력 바와 숫자를 표시한다.
- UMM 설정에서 HUD 크기·위치·텍스트·색상을 조절한다.

실패 처리 우선순위:

```text
게임의 실제 실패 방지 > PlanetGauge 체력 > 일반 게임오버
```

- 실제 `controller.noFail`이 켜졌으면 게임 실패 방지가 우선한다.
- 실제 실패 방지가 꺼졌고 체력이 남으면 PlanetGauge가 실패를 흡수한다.
- 체력이 소진되면 원본 `scrPlayer.Die()` 경로로 사망한다.
- `hitbox` 사망은 흡수하지 않는다.
- 실제 실패 방지 상태에서 체력이 바닥에 닿으면 최저 `-5`에서 동결한다.

## 4. 현재 게이지 규칙

`PlanetGauge/GaugeRuntime.cs`가 단일 기준이다.

| `HitMargin` | 변화량 |
|---|---:|
| `Perfect` | `+0.1` |
| `EarlyPerfect` | `-0.5` |
| `LatePerfect` | `-0.5` |
| `VeryEarly` | `-1.5` |
| `VeryLate` | `-1.5` |
| `TooEarly` | `-3` |
| `TooLate` | `0`, 즉시 반환 |
| `FailMiss` | `-8` |
| `FailOverload` | `-8` |
| `Multipress`, `Auto`, `OverPress`, 기타 | 직접 변화 없음 |

- 시작·최대 체력은 `100`이다.
- `Perfect` 회복으로 100을 넘지 않는다.
- `TooLate`는 중간 상태이므로 차감하지 않고, 이후 확정되는 `FailMiss`만 한 번 반영한다.

## 5. 파일 구조와 책임

```text
.
├─ DEVELOPMENT_HANDOFF.md
├─ PlanetGauge.slnx
├─ README.md
├─ dist/PlanetGauge/                 # 빌드 스테이징, Git ignore
└─ PlanetGauge/
   ├─ Main.cs
   ├─ GaugeRuntime.cs
   ├─ Patches.cs
   ├─ RuntimeHost.cs
   ├─ EditorGaugeButton.cs
   ├─ MainGaugeHud.cs
   ├─ GaugeBarGraphic.cs
   ├─ PlanetGaugeSettings.cs
   ├─ PlanetGauge.csproj
   ├─ Info.json
   └─ Properties/AssemblyInfo.cs
```

| 파일 | 역할 |
|---|---|
| `Main.cs` | UMM 진입점, 설정·토글·Harmony 수명, API 검사, 결과 및 놓침 복구 패치 |
| `GaugeRuntime.cs` | 체력 상태, 판정 변화량, 중복 차감 토큰, 복구 깊이, 강제 사망 |
| `Patches.cs` | 세션 초기화, `SwitchChosen` 판정, `scrPlayer.Die` 흡수 |
| `RuntimeHost.cs` | 에디터 감지와 UI 갱신을 연결하는 Unity 호스트 |
| `EditorGaugeButton.cs` | 에디터 게이지 토글 생성·배치·동기화 |
| `MainGaugeHud.cs` | 플레이 중 메인 게이지와 수치 텍스트 |
| `GaugeBarGraphic.cs` | 둥근/모따기 게이지 메시와 그라데이션 |
| `PlanetGaugeSettings.cs` | UMM 직렬화 설정과 IMGUI 화면 |
| `PlanetGauge.csproj` | 게임·Unity·UMM 참조와 `dist` 스테이징 |
| `Info.json` | UMM 패키지 메타데이터 |

과거의 `GaugeDebugOverlay.cs`는 현재 존재하지 않는다. `MainGaugeHud.cs`가 해당 역할을 대체한다.

## 6. 핵심 런타임 흐름

### 모드 수명

1. `Main.Load`가 설정과 UMM 콜백을 등록한다.
2. 모드를 켜면 필수 게임 API를 검사하고 Harmony 패치를 적용한다.
3. `RuntimeHost`가 에디터 버튼과 HUD를 갱신한다.
4. 모드를 끄면 UI를 제거하고 이 모드의 Harmony ID만 언패치한다.

### 세션 초기화

다음 네 경로가 각각 체력과 보류 상태를 초기화한다.

- `scnEditor.Play` Prefix
- `scnEditor.SwitchToEditMode` Postfix
- `scrController.Restart` Prefix
- `scrController.ResetCustomLevel` Prefix

### 판정 적용

`SwitchChosenPatch`:

1. Prefix에서 각도, 회전 방향, BPM·속도, pitch, `marginScale`로 판정을 계산한다.
2. 원본 `SwitchChosen`이 게임 상태와 실패 바를 갱신한다.
3. Postfix에서 `failBar.DidFail(false)`를 확인해 필요하면 `FailOverload`로 승격한다.
4. `TooLate`면 보류 토큰을 지우고 반환한다.
5. 확정 실패는 이어질 `Die`의 중복 차감을 막는 토큰을 표시한다.
6. `ApplyJudgement`가 체력을 변경하고 소진 시 `ForceDie`를 호출한다.

과거의 `IgnoreTooLateGaugePatch`는 현재 미커밋 변경에서 제거됐다. `TooLate`는 `SwitchChosenPatch.Postfix`에서 직접 처리한다.

### 놓침 복구

- `CheckPostHoldFailRecoveryPatch`는 체력이 남고 실제 실패 방지가 꺼진 동안만 `noFail`을 잠시 빌린다.
- `CheckPostHoldFail`은 버전 호환성을 위해 정확한 인자형 대신 이름으로 찾는다.
- 메서드가 없는 게임 버전에서는 해당 패치만 건너뛴다.
- `TemporaryNoFailDieBridgePatch`는 복구용 임시 `noFail`을 실제 설정과 구분한다.
- Postfix와 Finalizer 모두에서 임시 상태를 복원한다.

### 사망 흡수

`PlayerDiePatch`:

1. 강제 사망, hitbox, 적용 범위 밖, 자동 플레이를 제외한다.
2. 같은 실패가 이미 차감됐는지 토큰을 소비한다.
3. 아니면 `overload` 인자로 `FailOverload` 또는 `FailMiss`를 적용한다.
4. 체력이 남으면 바닐라 noFail 복구 경로를 잠시 빌린다.
5. 과부하는 noFail 분기에서 결과 판정을 남기지 않으므로 `AddHit(FailOverload)`를 한 번 기록한다.

## 7. 결과 화면 보정

현재 게임 구현은 일반 플레이에서 결과 화면의 `missFails`와 `overloadFails` 행을 항상 만들지 않는다. `DetailedResultsFailureRowsPatch`는 결과 문자열 생성 중에만 `noFail`을 빌려 해당 행을 표시하고 즉시 원복한다.

따라서 다음은 현재 유지 대상이다.

- `DetailedResultsFailureRowsPatch`
- 과부하 복구 경로의 `marginTracker.AddHit(FailOverload)`

게임 업데이트 후에는 현재 설치 DLL의 `DetailedResults.GenerateResults`와 `scrPlayer.Die`를 다시 확인한다.

## 8. Multipress와 과부하

`Multipress` 한 번은 체력을 직접 바꾸지 않는다. 원본 실패 바 누적을 사용한다.

```text
Multipress
→ 원본 OnDamage가 multipress 카운터 누적
→ SwitchChosen Postfix가 failBar.DidFail(false) 확인
→ 임계치 초과 시 FailOverload
→ 체력 -8
```

확인한 게임 구현에서는 연속 다중입력 카운터가 8을 넘으면 `Die(overload: true, multipress: true, ...)`를 직접 호출한다. `PlayerDiePatch`는 이때 `overload == true`를 보고 과부하로 분류한다.

### 잠재적 이중 차감

극단적인 연속 다중입력에서는 다음 순서가 가능하다.

```text
SwitchChosen 원본 안에서 Die(true, true)
→ PlayerDiePatch에서 -8
→ 바깥 SwitchChosen Postfix가 실패 바를 다시 확인
→ 추가 -8 가능성
```

현재 중복 방지 토큰은 주로 `SwitchChosen Postfix → 이후 Die` 순서를 방어한다. `Die → 바깥 Postfix` 순서는 실게임에서 검증되지 않았다. 연속 Multipress 9회 이상에서 체력이 정확히 8만 감소하는지 우선 확인한다.

수정한다면 토큰을 무작정 양방향으로 재사용하기보다 `SwitchChosen` 호출별 상태 또는 체력 변경 세대 번호처럼 방향이 명확한 방법을 사용한다.

## 9. 현재 미커밋 변경

| 파일 | 변경 |
|---|---|
| `Main.cs` | 미사용 `CurrentGauge`, 항상 true인 디버그 플래그, `IgnoreTooLateGaugePatch` 제거 |
| `GaugeRuntime.cs` | `TooLateDelta`와 전용 switch 분기 제거 |
| `Patches.cs` | `TooLate` 직접 처리, 미사용 using 제거, 오래된 `-18` 주석 수정 |
| `GaugeBarGraphic.cs` | 참조되지 않는 상태 getter 두 개 제거 |
| `MainGaugeHud.cs` | 도달 불가능한 설정 fallback 제거 |
| `PlanetGauge.csproj` | 불필요한 `Assembly-CSharp-firstpass`, 통합 `UnityEngine.dll` 참조 제거 |
| `AssemblyInfo.cs` | 미사용 using, 빈 메타데이터, 불필요한 COM GUID 제거 |
| `Info.json` | 버전을 `0.0.8`로 통일 |

`RDTools` 참조 제거도 시험했지만 게임 타입의 기반 클래스 `RDBaseDll`을 컴파일할 때 필요해 복원했다.

검증 명령:

```powershell
dotnet build PlanetGauge.slnx -c Release --no-restore -warnaserror
git diff --check
```

최근 결과는 경고 0개, 오류 0개다.

## 10. 전체 Git 이력

| 날짜 | 커밋 | 제목 |
|---|---|---|
| 2026-07-25 | `cf770b4` | `Initial commit` |
| 2026-07-25 | `08d3c1f` | `initialize commit` |
| 2026-07-25 | `5ad457a` | `어셈블리 미인식 오류 수정` |
| 2026-07-25 | `9784d62` | `게이지 업뎃` |
| 2026-07-25 | `352e0c5` | `배드말림 수정` |
| 2026-07-26 | `06e12e6` | `버그수정 2` |
| 2026-07-26 | `313d65b` | `게이지 버프` |
| 2026-07-26 | `88fb8ee` | `결과화면 놓침 과부하 기재` |
| 2026-07-26 | `4e99618` | `UI 구현` |
| 2026-07-26 | `34c0d25` | `UI 기본값 설정(1080p)` |
| 2026-07-26 | `02038db` | `Create README.md` |
| 2026-07-26 | `a33e0a3` | `Update README.md` |
| 2026-07-26 | `2ade381` | `기본개선` (`beta`) |
| 2026-07-26 | `2fce25d` | `Initial public release` |
| 2026-07-26 | `ece146d` | `Update README.md` — 생성형 AI 사용 고지 |
| 2026-07-26 | `444c758` | `AI잡티 제거` — 표시 이름·저자 정리 |
| 2026-07-26 | `5aabec9` | 원격 `main` 병합 |
| 2026-07-30 | `a82416c` | 자동 플레이 제외, 게이지 색과 UX 개선 |
| 2026-08-05 | `e191923` | `Info.json` 0.0.7 (`V0.0.7`) |
| 2026-08-05 | `8d6b299` | README 사용 방법 추가 |
| 2026-08-09 | `6d7cc69` | 고정 해시 검사 등 불필요 백엔드 제거 |

커밋 제목은 맥락 요약일 뿐이다. 회귀를 추적할 때는 `git show <commit>`으로 실제 diff를 확인한다.

## 11. 빌드와 배포

```powershell
dotnet build PlanetGauge.slnx -c Release
```

게임 설치 위치가 기본값과 다르면 `GameManagedDir` MSBuild 속성으로 그 PC의 Managed 폴더를 지정한다.

```powershell
dotnet build PlanetGauge.slnx -c Release `
  /p:GameManagedDir="<게임 Managed 폴더>"
```

빌드 후 갱신되는 파일:

```text
dist/PlanetGauge/PlanetGauge.dll
dist/PlanetGauge/Info.json
```

`PlanetGauge.zip`은 빌드 타깃이 자동 생성하지 않는다. 릴리스 전에 현재 DLL과 Info.json으로 ZIP을 새로 만들어야 한다. 게임 DLL과 Unity/UMM DLL은 `Private=false`이므로 배포물에 넣지 않는다.

현재 직접 참조:

- `0Harmony`
- `UnityModManager`
- `Assembly-CSharp`
- `RDTools`
- `UnityEngine.CoreModule`
- `UnityEngine.AudioModule`
- `UnityEngine.UIModule`
- `UnityEngine.IMGUIModule`
- `UnityEngine.UI`
- `Unity.TextMeshPro`

## 12. 실게임 테스트 체크리스트

1. UMM에서 모드가 오류 없이 켜지고 꺼지는가.
2. 에디터 진입 시 토글이 기본 OFF인가.
3. 에디터 1인 테스트에서만 메인 게이지가 보이는가.
4. 자동 플레이와 자동 타일에서 체력이 변하지 않는가.
5. 각 일반 판정이 현재 표의 값만큼 변하는가.
6. `TooLate` 자체는 체력을 바꾸지 않는가.
7. 자동 놓침 `FailMiss`가 정확히 한 번만 -8 되는가.
8. 놓침 복구 후 다음 타일로 정상 진행하는가.
9. 일반 과부하가 정확히 한 번만 -8 되는가.
10. 연속 Multipress 9회 이상에서도 -8 한 번만 적용되는가.
11. 체력 0에서 실제 사망하는가.
12. 실제 실패 방지의 우선순위와 -5 동결이 유지되는가.
13. 결과 화면의 놓침·과부하 행과 횟수가 정확한가.
14. 홀드 및 Pure Perfect 실패 경로가 정상인가.
15. 재시작·커스텀 리셋·편집 복귀에서 체력이 100으로 초기화되는가.
16. 모드를 끈 뒤 UI와 패치 동작이 남지 않는가.
17. UMM 설정 저장 후 HUD 설정이 유지되는가.

## 13. 다음 작업 우선순위

1. 현재 미커밋 정리 변경을 실게임 테스트한다.
2. 연속 Multipress 직접 사망 경로의 이중 차감 여부를 확인한다.
3. 결과 화면 판정 횟수가 각각 한 번만 기록되는지 확인한다.
4. 문제가 없으면 변경과 이 핸드오프를 커밋하고 새 배포 ZIP을 만든다.
5. 게임 업데이트 후에는 관련 API와 분기를 현재 설치 DLL에서 다시 확인한다.

## 다음 채팅용 지시문

```text
PlanetGauge 개발을 이어서 진행해줘.
저장소 루트의 DEVELOPMENT_HANDOFF.md를 읽고 현재 git status와 git diff를 먼저 확인해.
미커밋 정리 변경을 보존하고 Release 빌드를 검증해.
특히 연속 Multipress의 Die(true, true)와 SwitchChosen Postfix 사이 이중 차감을 실기 확인해.
```
