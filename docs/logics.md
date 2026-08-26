# PlanetGauge 핵심 구조 및 호환성 계약

최종 갱신: 2026-08-25. 구현 변경 시 지켜야 할 동작·직렬화 계약을 담는다. 버전별 이력·검증은 [updates.md](updates.md)를 참조한다.

## 설치본 DLL 기준선

빌드/API 검증 설치 경로는 `C:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice\`다.

| DLL | SHA-256 |
|---|---|
| `Assembly-CSharp.dll` | `0D1524DABF0F8C67AA58BA1992D6ED858522B9094490EBBB00E5E8AEFF7F40CD` |
| `UnityModManager/0Harmony.dll` | `DF043F05C6E43602FA8E8F38F52A3A2CF962AAB7A65A9D85573EA25F01A63E80` |
| `UnityModManager/UnityModManager.dll` | `5B4C5DA3896D4B353330315DC0AEEA06D984520BCA990B2B611ED86DA89D9003` |

게임 Mods 폴더의 DLL은 최신 Release로 덮어쓰지 않은 0.1.5 개발 빌드다: `AssemblyVersion 0.1.5.0`, 72,704바이트, SHA-256 `614B423D4F1D188EBFB7DC3AC992D635F1AB7893CA884AA25095B4B07088847E`.

중요 API는 `scrPlanet SwitchChosen()`, `scrPlayer.OnDamage(bool,bool,bool,HitMargin)`, `scrPlayer.Die(bool,bool,string,bool)`, `scrController.OnLandOnPortal(scrPlanet,Portal,string)`다. `SwitchChosen`의 정상 진행은 `movingToNext`, 조기·실패 반환은 `this`이므로 Auto 회복 성공은 `__result != null && __result != __instance`로 판별한다.

## 목적과 실패 계약

에디터 버튼이 켜진 1인 테스트 플레이에서만 판정·실패를 가로챈다. 실제 자동 플레이는 회복 디버깅만 허용하고 피해·사망 흐름은 가로채지 않는다.

```text
게임의 실제 no-fail > PlanetGauge 체력 > 원본 사망
```

- `hitbox` 사망은 흡수하지 않는다. 실제 no-fail 소진은 최저 `-5`에서 동결한다.
- `TooLate`는 직접 차감하지 않고 확정 `FailMiss`만 반영한다.
- SwitchChosen 내부 Die/Postfix의 중복 차감은 깊이별 관찰 상태·1회성 토큰으로 막는다.
- 실패 복구의 임시 no-fail은 중첩 깊이·Postfix·Finalizer로 반드시 복원한다.

| HitMargin | 변화량 |
|---|---:|
| Perfect | +0.1 |
| EarlyPerfect / LatePerfect | -0.5 |
| VeryEarly / VeryLate | -1.5 |
| TooEarly | -3 |
| FailMiss / FailOverload | -8 |

## 런타임 속성 모델

런타임은 단일 `AttributeMode` 대신 회복 차단, Blindfold 활성·세션 공개 상태, 회복/피해 유효 채널(활성·퍼센트·출처), Increase/Decrease/Both별 마지막 퍼센트, 실패 방지·상한·Auto 설정을 보관한다.

겹치는 배율은 곱하지 않고 각 부호의 마지막 명령이 이전 채널을 상쇄한다.

```text
Increase 150 → Both 200 = 회복 200(Both), 피해 200(Both)
Both 200 → Increase 150 = 회복 150(Increase), 피해 200(Both)
Both 200 → Decrease 50  = 회복 200(Both), 피해 50(Decrease)
```

- `0..99.999%` Reduced, `100%` 중립(토큰·문구 숨김), 초과 Amplified, 입력은 `0..1000%`.
- 속성 끄기와 `다른 속성 설정 끄기`는 활성 상태만 끄며 저장 퍼센트는 보존한다. 일반 + 후자는 모든 활성 상태를 끈다.
- 회복 차단은 배율과 독립이며 최종 양수 변화량을 0으로 만든다.

## SetPlanetGauge 직렬화·에디터 계약

변경 금지:

```text
eventType = SetPlanetGauge
numeric ID = 20551
attributeMode, multiplierPercent, failureProtection,
recoveryCapEnabled, recoveryCapPercent, forceRecoveryCap
```

O/X는 `LevelEvent.disabled`만 해석한다. Encode Postfix는 `eventType`을 `SetPlanetGauge`로 저장하며 숫자 ID의 `ToString()`을 JSON에 쓰지 않는다.

| 키 | 기본값 | 형식/계약 |
|---|---:|---|
| `attributeEnabled` | true | 선택 속성 켜기 Bool |
| `disableOtherAttributes` | false | 다른 속성 설정 끄기 Bool |
| `autoTileRecovery` | false | 선택형 Bool, 새 이벤트 기본 X |
| `recoveryAmountPercent` | 0 | ForceRecovery 전용, Float `-1000..1000%` |
| `warningOffsetAngle` | 0 | ForceRecovery 전용, 유한 `<=0°`; 양수·비유한수는 0 |
| `warningPulseBeats` | 0.5 | ForceRecovery 전용, Float `0.125..16`, `unit="beats"` |

0.1.1 JSON에 새 Bool이 없으면 Decode 기본값은 `true`, `false`, `false`다. 회복 상한은 `0.1..1000`이다. enum 순서는 반드시 `Normal=0, BlockRecovery=1, AmplifyDecrease=2, AmplifyIncrease=3, AmplifyBoth=4, Blindfold=5, ForceRecovery=6`을 지킨다.

Blindfold는 `attributeMode: "Blindfold"`로 저장한다. ForceRecovery는 `attributeEnabled`를 숨기고 기존 `attributeMode` O/X가 실행 여부를 정한다. 경고·회복량 키는 별도 O/X가 없다. `warningOffsetAngle=0`은 경고만 끄며 네이티브 `angleOffset` 의미는 보존한다.

## Auto·상한·ForceRecovery

- 수동 Auto 타일은 `autoTileRecovery=true`일 때만, 실제 Auto(`RDC.auto` 또는 `player.auto`)는 항상 정상 진행마다 회복한다.
- 두 경로는 차단·회복 배율·상한을 거치고 실제 적용량만 Auto 누계에 더하며 `ApplyJudgement`·`Die` 가로채기를 호출하지 않는다.
- 상한 ON이면 바 정규화 기준은 상한, OFF에서 100 초과면 바는 가득·숫자는 실제값이다. 강제 제한은 상한 초과 시에만 즉시 낮춘다.
- ForceRecovery는 현재 체력에 한 번 직접 적용하며 차단·배율은 우회하고 상한·사망·no-fail은 따른다. 선행 경고·`0.75초 OutCirc` 오버레이는 시각 전용이고 실제 체력·숫자·사망은 이벤트 시점에 확정한다.

## HUD·Blindfold·디버그

HUD 상태색은 `0.5초 EaseOutQuad`로 보간하며 `Time.unscaledDeltaTime`을 쓴다. Blindfold는 바를 검정·숫자를 `???`로 만들며 사망/hitbox/no-fail 0 이하 동결에서 숫자만 공개한다. 포털 착지 Prefix는 Blindfold만 해제한다.

토큰은 바 위 독립 위치에 둔다. Both 출처·퍼센트가 양쪽에 같으면 하나만 보이고 100%·비활성은 숨긴다. 증폭색은 Increase/Decrease/Both 순으로 `#45D66B/#FF9F1C/#FFE36E`, 감쇠색은 `#3CCFCF/#5DADE2/#B794F4`다. `MainGaugeSizePercent`는 `25..200%`; 기존 폭·값 크기 키는 보존한다.

Shift+F3 HUD는 play/pause에서 보이고 edit에서 숨는다. restart/reset은 게이지·누계만 초기화, edit 복귀는 표시 상태도 초기화한다. 10Hz로 Gauge/채널/상한/Auto/사망 상태/Active와 판정별·Auto 실제 적용량 누계를 출력한다. 숫자는 invariant `0.###`, 누계는 `+0.###;-0.###;0`이다.

## 주요 파일과 빌드

| 파일 | 책임 |
|---|---|
| `GaugeRuntime.cs` | 체력, 채널, 판정/Auto 적용량·누계, 사망 상태 |
| `GaugeVisualTransitions.cs` | ForceRecovery 경고·적용 전환 |
| `Patches.cs` | 세션, SwitchChosen 관찰, Die 흡수 |
| `PlanetGaugeLevelEvent.cs` | 이벤트 스키마·직렬화·등록·현지화 |
| `MainGaugeHud.cs` | 게이지·숫자·토큰·색 전환 |
| `PlanetGaugeSettings.cs` | UMM 설정·값 보정 |
| `GaugeDebugHud.cs` | Shift+F3 HUD |
| `RuntimeHost.cs` | HUD 수명·입력 |
| `GaugeBarGraphic.cs` | 게이지 메시 |
| `PlanetGauge.csproj` | DLL 참조·dist 스테이징 |

`UnityEngine.InputLegacyModule.dll`은 Shift+F3 입력 때문에 명시 참조하며 `Private=false`다.

```powershell
dotnet build PlanetGauge/PlanetGauge.csproj -c Release
```

빌드는 `dist/PlanetGauge/PlanetGauge.dll`, `Info.json`, `Assets/Gaugeline.png`를 자동 스테이징한다.
