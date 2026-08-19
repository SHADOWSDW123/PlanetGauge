# PlanetGauge 개발 인수인계

최종 갱신: 2026-08-19

## 1. 현재 스냅샷

| 항목 | 값 |
|---|---|
| 브랜치 | `main` (`origin/main`보다 1커밋 앞섬) |
| 작업 시작 HEAD | `0eb8291` (`0.1.1 테스트 / 대체 뭐가 바뀐거임... 오버엔지니어링같은데`) |
| 현재 소스 버전 | `0.1.2` |
| 타깃 | .NET Framework 4.8 / C# 7.3 |
| 전용 이벤트 | `SetPlanetGauge`, `0x5047` (`20551`) |
| 최신 Release | 경고 0개, 오류 0개 |
| Release DLL | 62,976바이트, SHA-256 `C6987C96766138BF93674E4DE09DA36FC7F31DB8ABEBD73546DBE88C524135C9` |

이 문서 갱신 시점에는 0.1.2 구현이 아직 커밋되지 않은 작업 트리에 있다. 다음 작업자는 반드시 먼저 확인한다.

```powershell
git status --short --branch
git diff --check
git diff
```

### 이번 활동 요약

이번 작업은 0.1.1을 기준으로 0.1.2 기능을 구현하고, 설치본 DLL을 참조한 Release 산출물까지 스테이징한 활동이다.

- 단일 속성 모드를 회복/피해 유효 채널과 독립 회복 차단 상태로 교체했다.
- 증가, 감소, Both의 설정 퍼센트를 각각 기억하고, 겹치는 부호에서는 마지막 명령만 유효하도록 했다. 배율끼리 곱하지 않는다.
- 이벤트에 `attributeEnabled`, `disableOtherAttributes`, `autoTileRecovery`를 추가하면서 기존 이벤트 ID, 이름, 키, O/X 해석을 유지했다.
- 증감률 100% 미만을 Reduced, 초과를 Amplified로 구분하고 체력 숫자 아래에 출처색 퍼센트 토큰을 추가했다.
- 회복 상한을 1000%까지 확장하고 100을 넘는 체력 숫자/바 정규화를 반영했다.
- 수동 Auto 타일과 실제 Auto의 회복 경로를 추가하되 기존 Auto 판정·사망 흐름과 분리했다.
- 메인 게이지와 체력 숫자 색을 0.5초 OutQuad로 보간하도록 했다.
- `Shift+F3` 디버그 HUD를 새로 만들고 상태 변수와 판정별 실제 적용량 누계를 표시한다.
- `Info.json`, AssemblyVersion, AssemblyFileVersion을 0.1.2로 맞추고 README 및 이 문서를 갱신했다.
- 설치본 DLL 기준 Release 빌드와 임시 reflection 채널 계약 검사는 완료했지만 게임을 직접 실행하지 않았으므로 실게임 검증은 남아 있다.

## 2. 설치본 DLL 기준선

빌드와 API 검증에 사용한 설치 경로:

```text
C:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice\
```

| DLL | SHA-256 |
|---|---|
| `Assembly-CSharp.dll` | `0D1524DABF0F8C67AA58BA1992D6ED858522B9094490EBBB00E5E8AEFF7F40CD` |
| `UnityModManager/0Harmony.dll` | `DF043F05C6E43602FA8E8F38F52A3A2CF962AAB7A65A9D85573EA25F01A63E80` |
| `UnityModManager/UnityModManager.dll` | `5B4C5DA3896D4B353330315DC0AEEA06D984520BCA990B2B611ED86DA89D9003` |

설치본에서 확인한 중요 API:

- `scrPlanet SwitchChosen()`
- `scrPlayer.OnDamage(bool,bool,bool,HitMargin)`
- `scrPlayer.Die(bool,bool,string,bool)`
- `SwitchChosen`의 조기/실패 반환은 `this`, 정상 진행 반환은 `movingToNext`다.
- 따라서 Auto 회복 성공 판별은 `__result != null && __result != __instance`를 사용한다.

## 3. 목적과 기존 실패 계약

PlanetGauge는 레벨 에디터의 1인 테스트 플레이에 체력 게이지를 추가한다. 에디터 버튼이 켜진 테스트 플레이에서만 판정/실패를 가로챈다. 실제 자동 플레이는 0.1.2부터 회복 디버깅만 허용하며, 피해·사망 흐름은 절대 가로채지 않는다.

실패 우선순위는 계속 다음과 같다.

```text
게임의 실제 no-fail > PlanetGauge 체력 > 원본 사망
```

- `hitbox` 사망은 흡수하지 않는다.
- 실제 no-fail에서 체력이 소진되면 최저 `-5`에서 동결한다.
- `TooLate`는 직접 차감하지 않고 뒤의 확정 `FailMiss`만 반영한다.
- `SwitchChosen` 내부 `Die`와 Postfix 사이 중복 차감은 깊이별 관찰 상태와 1회성 토큰으로 막는다.
- 실패 복구의 임시 no-fail은 중첩 깊이, Postfix, Finalizer로 반드시 복원한다.

기본 변화량:

| HitMargin | 변화량 |
|---|---:|
| Perfect | +0.1 |
| EarlyPerfect / LatePerfect | -0.5 |
| VeryEarly / VeryLate | -1.5 |
| TooEarly | -3 |
| FailMiss / FailOverload | -8 |

## 4. 0.1.2 속성 모델

내부 enum/기존 JSON 값은 호환성을 위해 그대로지만 화면 이름은 다음과 같이 바뀌었다.

| 내부 값 | 화면 이름 |
|---|---|
| `AmplifyIncrease` | 증가율 변경 |
| `AmplifyDecrease` | 감소율 변경 |
| `AmplifyBoth` | 증가·감소율 변경 |

런타임은 단일 `AttributeMode`가 아니라 다음 상태를 보관한다.

- 회복 차단: 독립 Bool
- 회복 유효 채널: 활성 여부, 퍼센트, 출처(`Increase`/`Both`)
- 피해 유효 채널: 활성 여부, 퍼센트, 출처(`Decrease`/`Both`)
- 증가/감소/Both별 마지막 설정 퍼센트: 각각 독립 보존
- 실패 방지, 회복 상한, Auto 타일 회복

겹치는 배율은 곱하지 않는다. 각 부호에서 마지막 명령이 이전 유효 채널을 상쇄한다.

```text
Increase 150 → Both 200       = 회복 200(Both), 피해 200(Both)
Both 200 → Increase 150       = 회복 150(Increase), 피해 200(Both)
Both 200 → Decrease 50        = 회복 200(Both), 피해 50(Decrease)
```

- `0..99.999%`: Reduced
- `100%`: 중립. 배율 토큰과 상태 문구를 숨긴다.
- `100% 초과`: Amplified
- 입력 범위: `0..1000%`
- 선택 속성 끄기는 그 속성과 겹치는 유효 부호 채널을 끄지만 저장된 퍼센트는 보존한다.
- `다른 속성 설정 끄기`는 모든 속성 활성 상태를 먼저 끄고 선택 속성을 적용한다. 저장된 퍼센트 메모리는 보존한다.
- 회복 차단은 배율 채널과 별개이며 최종 양수 변화량을 0으로 만든다.

## 5. SetPlanetGauge 직렬화 계약

절대 변경하지 말아야 할 기존 식별자/키:

```text
eventType = SetPlanetGauge
numeric ID = 20551
attributeMode
multiplierPercent
failureProtection
recoveryCapEnabled
recoveryCapPercent
forceRecoveryCap
```

0.1.2 추가 키:

| 표시 이름 | 키 | 기본값 | 형식 |
|---|---|---:|---|
| 선택 속성 켜기 | `attributeEnabled` | true | Bool |
| 다른 속성 설정 끄기 | `disableOtherAttributes` | false | Bool |
| 자동 플레이 타일 체력 회복 | `autoTileRecovery` | false | 선택형 Bool, 새 이벤트 기본 X |

기존 속성:

| 표시 이름 | 키 | 새 이벤트 O/X |
|---|---|---|
| 속성 설정 | `attributeMode` | O |
| 변경값 설정 | `multiplierPercent` | X |
| 실패 방지 | `failureProtection` | X |
| 회복 상한 설정 | `recoveryCapEnabled` | X |
| 회복 상한 | `recoveryCapPercent` | 그룹 하위 |
| 체력 상한 강제 제한 | `forceRecoveryCap` | 그룹 하위 |

0.1.1 JSON에는 새 키가 없으므로 Decode 기본값을 `attributeEnabled=true`, `disableOtherAttributes=false`, `autoTileRecovery=false`로 둔다. O/X는 계속 `LevelEvent.disabled`에서만 해석한다. 증감률 모드를 직접 고르면 기존처럼 `multiplierPercent` O/X를 바닐라 버튼 경로로 O로 바꾼다.

`LevelEvent.Encode` Postfix는 `eventType`을 다시 `SetPlanetGauge`로 저장한다. 숫자 ID의 `ToString()`을 JSON에 내보내면 안 된다. 회복 상한 범위는 0.1.2에서 `0.1..1000`으로 확장됐다.

## 6. Auto 회복

- 수동 플레이의 Auto 타일: `autoTileRecovery`가 켜졌을 때만 정상 진행 타일당 `PerfectDelta`를 회복한다.
- 실제 자동 플레이(`RDC.auto` 또는 player.auto): 설정과 무관하게 정상 진행 타일당 회복한다.
- 두 경우 모두 회복 차단, 회복 배율, 회복 상한을 거친다.
- 실제 적용량만 디버그 `Auto` 누계에 더한다.
- Auto 경로에서는 `ApplyJudgement`와 `Die` 가로채기를 호출하지 않는다.

## 7. 회복 상한과 HUD

- 회복 상한 범위는 `0.1..1000`이다.
- 숫자 체력은 100을 넘어도 실제 값을 표시한다.
- 상한이 켜지면 바 정규화 기준은 활성 상한이다.
- 상한이 꺼진 상태에서 체력이 100보다 높으면 바는 가득 차고 숫자는 실제 값을 표시한다.
- 강제 제한은 이벤트 실행 시 현재 체력이 상한보다 높은 경우에만 즉시 낮춘다.

HUD 상태색은 0.5초 동안 `EaseOutQuad` (`1-(1-t)^2`)로 보간한다. `Time.unscaledDeltaTime`을 사용하므로 일시정지 중에도 전환이 완료된다. 중간에 목표가 바뀌면 현재 보간색에서 새 목표로 이동한다.

체력 숫자 아래 배율 토큰:

- 회복/피해 유효 채널을 각각 출처색으로 표시한다.
- 동일한 Both 출처와 퍼센트가 양쪽에 남아 있으면 한 번만 표시한다.
- 100%와 비활성 채널은 숨긴다.
- 증폭색: Increase `#45D66B`, Decrease `#FF9F1C`, Both `#FFE36E`
- 감쇠색: Increase `#3CCFCF`, Decrease `#5DADE2`, Both `#B794F4`

상태 문구는 `Increase/Decrease/Rate Amplified` 또는 `Reduced`를 유효 채널 기준으로 만든다. 회복 차단, 실패 방지 꺼짐, 회복 상한 문구는 기존 계약을 유지한다.

## 8. Shift+F3 디버그 HUD

- 플레이 중 `Shift+F3`로 좌상단 표시를 토글한다.
- 첫 줄은 항상 `Shift+F3`다.
- 플레이 일시정지와 실제 Auto 일시정지에서도 숨기지 않는다.
- 편집 화면에서는 숨기며, 편집 모드 복귀/에디터 장면 진입 시 표시 상태를 초기화한다.
- Restart/ResetCustomLevel은 게이지·누계만 초기화하고 표시 토글은 유지한다.
- 10Hz로 텍스트를 갱신한다.

### 화면에 기재되는 전체 행

`GaugeDebugHud.BuildText()`가 만드는 실제 순서와 표기는 다음과 같다.

```text
Shift+F3
Gauge: <Current> / <RecoveryMaximum>  BaseMax: <MaximumGauge>
Recovery: <RecoveryRate>  Damage: <DamageRate>
BlockRecovery: <RecoveryBlocked>  FailureProtection: <FailureProtection>
RecoveryCap: <RecoveryCapEnabled> @ <RecoveryCapPercent>  AutoTileRecovery: <AutoTileRecovery>
ActualAutoPlay: <IsAutoPlay()>  Paused: <controller.paused>
Frozen: <IsFrozen>  RecoveryDepth: <FailureRecoveryDepth>  PendingDie: <HasPendingDieCharge>  ForcingDeath: <IsForcingDeath>
Active: <활성 상태 목록 또는 None>
Totals (applied)
TooEarly: <누계>
VeryEarly: <누계>
EarlyPerfect: <누계>
Perfect: <누계>
LatePerfect: <누계>
VeryLate: <누계>
FailMiss: <누계>
FailOverload: <누계>
Auto: <누계>
```

### 변수와 계산값의 정확한 의미

| 화면 표기 | 실제 소스 | 값과 의미 |
|---|---|---|
| `Shift+F3` | 고정 문자열 | 단축키 안내. 항상 첫 줄이다. |
| `Gauge` 첫 값 | `GaugeRuntime.Current` | 현재 실제 체력. 소수 셋째 자리까지 표시하며 100을 넘을 수 있다. |
| `Gauge` 두 번째 값 | `GaugeRuntime.RecoveryMaximum` | 현재 양수 회복에 적용되는 상한. `RecoveryCapEnabled=true`이면 보정된 `RecoveryCapPercent`, 아니면 기본 100이다. |
| `BaseMax` | `GaugeRuntime.MaximumGauge` | 기본 게이지 최대값 상수. 현재 `100`. 활성 회복 상한과 구별하기 위해 별도로 표시한다. |
| `Recovery` | `EventSettings.RecoveryRate` | 양수 변화에 개입하는 마지막 유효 채널. 활성 시 `<Source> <Percent>%`, 비활성 시 `Default 100%`. |
| `Damage` | `EventSettings.DamageRate` | 음수 변화에 개입하는 마지막 유효 채널. 활성 시 `<Source> <Percent>%`, 비활성 시 `Default 100%`. |
| 채널 `Source` | `PlanetGaugeRateChannel.Source` | `Increase`, `Decrease`, `Both` 중 현재 채널의 출처. 비활성 채널은 화면에서 Source 대신 `Default 100%`로 합쳐 표시한다. |
| 채널 `Percent` | `PlanetGaugeRateChannel.Percent` | 실제 해당 부호 변화량에 적용되는 퍼센트. 100%도 채널이 활성 상태라면 디버그에는 표시된다. |
| `BlockRecovery` | `EventSettings.RecoveryBlocked` | `True`이면 모든 양수 회복을 배율 계산과 무관하게 최종 0으로 만든다. |
| `FailureProtection` | `EventSettings.FailureProtection` | PlanetGauge가 `FailMiss`/`FailOverload`를 체력으로 흡수할지 여부. 게임의 실제 no-fail이 더 높은 우선순위다. |
| `RecoveryCap` | `EventSettings.RecoveryCapEnabled` | 이벤트 회복 상한 사용 여부. |
| `RecoveryCap @` 뒤 값 | `EventSettings.RecoveryCapPercent` | 저장된 회복 상한 설정값. 상한이 꺼져 있어도 기억된 값 자체는 계속 표시된다. 범위는 `0.1..1000`. |
| `AutoTileRecovery` | `EventSettings.AutoTileRecovery` | 수동 플레이에서 Auto 타일 정상 진행 시 체력을 회복할지 여부. 실제 Auto 회복에는 이 Bool이 필요하지 않다. |
| `ActualAutoPlay` | `GaugeRuntime.IsAutoPlay()` | `RDC.auto` 또는 현재 1P의 `player.auto`가 켜졌는지 계산한 값. |
| `Paused` | `scrController.instance.paused` | 현재 컨트롤러의 일시정지 상태. 컨트롤러가 없으면 `False`. HUD는 `True`여도 숨지 않는다. |
| `Frozen` | `GaugeRuntime.IsFrozen` | 체력이 소진된 후 더 이상 변화시키지 않는 동결 상태. 실제 no-fail 바닥에서도 사용한다. |
| `RecoveryDepth` | `GaugeRuntime.FailureRecoveryDepth` | 원본 실패 복구 경로에 빌린 임시 no-fail의 중첩 깊이. 정상 대기 상태는 `0`. |
| `PendingDie` | `GaugeRuntime.HasPendingDieCharge` | `SwitchChosen`에서 이미 실패 차감을 적용했고 뒤따르는 `Die`가 같은 실패인지 나타내는 1회성 토큰. |
| `ForcingDeath` | `GaugeRuntime.IsForcingDeath` | 게이지 소진 후 모드가 원본 `scrPlayer.Die()`를 직접 호출 중인지 나타내는 재진입 방지 상태. |
| `Active` | `BuildActiveList(EventSettings)` | 아래 활성 상태 이름을 정해진 순서로 쉼표 구분해 표시한다. 아무것도 없으면 `None`. |
| `Totals (applied)` | 고정 문자열 | 아래 값들이 원시 판정값이 아니라 최종 실제 체력 적용량 누계임을 나타내는 제목. |
| `TooEarly` | `GetJudgementTotal(HitMargin.TooEarly)` | 매우 빠름 판정의 최종 실제 적용량 누계. |
| `VeryEarly` | `GetJudgementTotal(HitMargin.VeryEarly)` | 빠름! 판정의 최종 실제 적용량 누계. |
| `EarlyPerfect` | `GetJudgementTotal(HitMargin.EarlyPerfect)` | 빠름 판정의 최종 실제 적용량 누계. |
| `Perfect` | `GetJudgementTotal(HitMargin.Perfect)` | 정확 판정의 최종 실제 적용량 누계. |
| `LatePerfect` | `GetJudgementTotal(HitMargin.LatePerfect)` | 느림 판정의 최종 실제 적용량 누계. |
| `VeryLate` | `GetJudgementTotal(HitMargin.VeryLate)` | 느림! 판정의 최종 실제 적용량 누계. |
| `FailMiss` | `GetJudgementTotal(HitMargin.FailMiss)` | 놓침 판정의 최종 실제 적용량 누계. |
| `FailOverload` | `GetJudgementTotal(HitMargin.FailOverload)` | 과부하 판정의 최종 실제 적용량 누계. |
| `Auto` | `GaugeRuntime.AutoTotal` | 수동 Auto 타일 또는 실제 Auto가 정상 진행해 적용한 최종 회복량 누계. |

`Active` 목록에는 다음 이름만 들어가며, 아래 순서를 유지한다.

| 표시 이름 | 포함 조건 |
|---|---|
| `BlockRecovery` | `RecoveryBlocked == true` |
| `RecoveryRate` | `RecoveryRate.Enabled == true` |
| `DamageRate` | `DamageRate.Enabled == true` |
| `NoFailDisabled` | `FailureProtection == false` |
| `RecoveryCap` | `RecoveryCapEnabled == true` |
| `AutoTileRecovery` | `AutoTileRecovery == true` |

### 표시 형식과 누계 규칙

- 일반 수치는 invariant culture의 `0.###` 형식이다. 소수점 아래 불필요한 0은 생략한다.
- 판정/Auto 누계는 `+0.###;-0.###;0` 형식이라 양수에는 `+`, 음수에는 `-`가 붙는다.
- Bool은 C# 기본 문자열인 `True`/`False`로 표시된다.
- 누계는 회복 차단, 유효 배율, 회복 상한, no-fail 바닥/0 체력 제한을 모두 거친 `Current`의 실제 전후 차이만 더한다.
- 예를 들어 원시 회복 +0.1 중 상한 때문에 +0.03만 들어갔다면 +0.03만, 회복 차단으로 0이 됐다면 0만 반영된다.
- `FailureProtection=false`가 원본 사망으로 바로 보내 게이지를 바꾸지 않은 실패는 해당 판정 누계도 증가하지 않는다.
- 마지막 판정, 판정 직전 체력, 판정 직후 체력은 사용자 결정에 따라 의도적으로 표시하지 않는다.
- `GaugeRuntime.Reset()`은 체력, 이벤트 상태, 판정 누계, Auto 누계, frozen/Die/복구 상태를 초기화한다. HUD의 `Visible`은 별도 소유라 Restart에서는 유지하고 편집 화면 복귀에서만 초기화한다.

## 9. 주요 파일

| 파일 | 책임 |
|---|---|
| `GaugeRuntime.cs` | 체력, 채널 상태, 판정/Auto 실제 변화, 누계, 사망 상태 |
| `Patches.cs` | 세션 경계, SwitchChosen 판정/Auto 성공 관찰, Die 흡수 |
| `PlanetGaugeLevelEvent.cs` | 이벤트 스키마, Decode/Encode, 등록, 아이콘, 현지화 |
| `MainGaugeHud.cs` | 게이지/숫자/배율/효과 문구와 색 전환 |
| `GaugeDebugHud.cs` | Shift+F3 좌상단 디버그 표시 |
| `RuntimeHost.cs` | 버튼, 메인 HUD, 디버그 HUD 수명과 입력 |
| `GaugeBarGraphic.cs` | 게이지 메시 |
| `PlanetGauge.csproj` | 설치 DLL 참조와 dist 스테이징 |

`UnityEngine.InputLegacyModule.dll`은 Shift+F3 입력 때문에 명시적으로 참조하며 `Private=false`다.

## 10. 빌드와 완료된 검증

```powershell
dotnet build PlanetGauge/PlanetGauge.csproj -c Release
```

빌드가 자동 스테이징하는 파일:

```text
dist/PlanetGauge/PlanetGauge.dll
dist/PlanetGauge/Info.json
dist/PlanetGauge/Assets/Gaugeline.png
```

완료:

- 위 설치본 DLL 기준 Release 빌드: 경고 0, 오류 0.
- `Info.json` 0.1.2, AssemblyVersion/FileVersion 0.1.2.0 일치.
- dist에는 DLL, Info, Gaugeline만 존재하며 게임/Unity/Harmony DLL은 복사되지 않음.
- 임시 reflection 실행 검증으로 마지막 채널 우선, Both 부분 덮어쓰기, 다른 속성 끄기, 속성별 변경값 재사용 계약 통과.
- `git diff --check` 오류 없음(Windows CRLF 안내만 존재).

## 11. 반드시 남은 실게임 검증

빌드/정적 검증은 실게임 동작을 보장하지 않는다. 다음은 아직 수동 확인이 필요하다.

1. 0.1.1 레벨 JSON 로드 → 실행 → 저장 → 재로드에서 기존 키와 O/X가 보존되는가.
2. 새 Bool 두 개와 Auto 토글의 표시, 복사, 삭제, Undo/Redo가 정상인가.
3. Increase/Both/Decrease 순서를 바꾼 위 세 예제에서 실제 회복·피해가 마지막 채널 규칙과 일치하는가.
4. 속성 끄기와 `다른 속성 설정 끄기`가 활성 상태만 끄고 각 변경값 메모리를 보존하는가.
5. 0%, 50%, 100%, 150%, 1000%에서 수치, Reduced/Amplified 문구, 토큰 색이 맞는가.
6. 100% 토큰/문구가 숨겨지고 Both 동일 채널이 하나의 토큰으로 합쳐지는가.
7. 회복 상한 150/500/1000에서 숫자와 바가 깨지지 않는가. 상한 해제 후 100 초과 숫자가 유지되는가.
8. 수동 Auto 타일 토글 OFF/ON과 실제 Auto에서 정상 진행당 한 번만 회복하는가.
9. Auto 회복이 차단/배율/상한을 통과하면서 피해·사망 패치를 건드리지 않는가.
10. HUD 색이 0.5초 OutQuad로 전환되고 연속 이벤트 중 튀지 않는가.
11. Shift+F3가 play/pause에서 보이고 edit에서 숨으며, restart에는 표시 유지·누계 초기화, edit 복귀에는 표시 초기화되는가.
12. 디버그 판정 누계와 Auto 누계가 상한/차단 후 실제 적용량과 일치하는가.
13. 실패 방지 OFF, 실제 no-fail, hitbox, 연속 Multipress 9회 이상에서 기존 판정·사망 우선순위와 단일 차감이 유지되는가.
14. 다양한 해상도/UI 배율에서 체력 숫자, 배율 토큰, 여러 효과 줄, 디버그 HUD가 겹치거나 잘리지 않는가.
15. 이벤트 아이콘, requiredMods, PACL2/JALib 공존이 기존처럼 정상인가.

## 다음 채팅용 지시문

```text
PlanetGauge 0.1.2 작업을 이어서 진행해줘.
저장소 루트 DEVELOPMENT_HANDOFF.md를 전부 읽고 git status와 현재 설치 DLL을 먼저 확인해.
기존 판정·사망 흐름과 SetPlanetGauge 이름/ID/기존 JSON 키/O-X 계약을 보존해.
Release 빌드 검증과 아직 필요한 실게임 검증을 구분해서 보고해.
```
