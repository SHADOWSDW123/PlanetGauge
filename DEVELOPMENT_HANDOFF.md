# PlanetGauge 개발 인수인계

최종 갱신: 2026-08-19

대상 게임: A Dance of Fire and Ice(얼불춤)

모드 로더: Unity Mod Manager + Harmony

저장소 기준 경로: 이 문서가 있는 저장소 루트

PC별 절대 경로와 설치 DLL 해시는 장기 호환성 근거로 사용하지 않는다. 다음 작업자는 현재 체크아웃한 소스, 현재 설치된 게임 DLL, 실게임 재현 결과 순서로 사실을 다시 확인한다.

## 1. 현재 스냅샷

| 항목 | 현재 값 |
|---|---|
| 브랜치 | `main` |
| HEAD | `1391f2e` (`Merge branch 'main' of https://github.com/SHADOWSDW123/PlanetGauge`) |
| 원격 | `origin/main` = `1391f2e` |
| 모드 버전 | `0.1.0` |
| 타깃 | .NET Framework 4.8 / C# 7.3 |
| 최신 빌드 | Release, 경고 0개·오류 0개 |
| 자동 테스트 프로젝트 | 없음 |
| 전용 이벤트 | `SetPlanetGauge`, 숫자 ID `0x5047` (`20551`) |

`88a772e`에 커스텀 이벤트, 런타임 동작, HUD 상태색/문구, 전용 아이콘까지 포함되었고, 현재 HEAD는 이 커밋을 포함한 `1391f2e`다. 2026-08-19 현재 `Patches.cs`의 Multipress 중복 차감 방지와 이 문서가 미커밋 변경으로 남아 있다.

다음 작업 시작 시 먼저 실행한다.

```powershell
git status --short --branch
git diff
```

## 2. 모드 목적과 적용 범위

PlanetGauge는 레벨 에디터의 1인 테스트 플레이에 체력형 게이지를 추가한다.

- 에디터의 작은 게이지 버튼으로 PlanetGauge 동작을 켜고 끈다.
- 새 에디터 세션에서는 기본 OFF다.
- 에디터의 1인 실제 플레이에서만 판정과 실패 흐름을 변경한다.
- 일반 플레이, 협동 플레이, 자동 플레이에는 적용하지 않는다.
- 판정 오차 미터 위에 체력 바, 숫자, 활성 효과 문구를 표시한다.
- 레벨 에디터의 `PlanetGauge 설정` 이벤트로 플레이 중 규칙을 변경한다.
- 게이지 스킨 기능은 아직 구현하지 않았다.

실패 처리 우선순위는 다음과 같다.

```text
게임의 실제 실패 방지 > PlanetGauge 체력 > 일반 게임오버
```

- 실제 `controller.noFail`이 켜졌으면 바닐라 실패 방지가 우선한다.
- PlanetGauge의 실패 방지가 켜져 있고 체력이 남으면 실패를 흡수한다.
- 체력이 소진되거나 이벤트로 PlanetGauge 실패 방지를 끄면 원본 `scrPlayer.Die()` 경로를 사용한다.
- `hitbox` 사망은 흡수하지 않는다.
- 실제 실패 방지 상태에서 체력이 바닥에 닿으면 최저 `-5`에서 동결한다.

## 3. 파일 구조와 책임

```text
.
├─ DEVELOPMENT_HANDOFF.md
├─ PlanetGauge.slnx
├─ README.md
├─ dist/PlanetGauge/                     # 빌드 스테이징, Git ignore
│  ├─ PlanetGauge.dll
│  ├─ Info.json
│  ├─ Assets/Gaugeline.png
│  └─ PlanetGauge.zip                    # 릴리스 시 수동 생성; 현재는 없음
└─ PlanetGauge/
   ├─ Assets/Gaugeline.png               # 커스텀 이벤트 아이콘 원본
   ├─ Main.cs
   ├─ GaugeRuntime.cs
   ├─ Patches.cs
   ├─ PlanetGaugeLevelEvent.cs
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
| `Main.cs` | UMM 진입점, 모드 경로, 설정·토글·Harmony 수명, API 검사, 결과/놓침 복구 패치 |
| `GaugeRuntime.cs` | 체력과 현재 이벤트 설정의 단일 기준, 판정 변화, 강제 사망 |
| `Patches.cs` | 세션 초기화, `SwitchChosen` 판정, `scrPlayer.Die` 흡수 |
| `PlanetGaugeLevelEvent.cs` | 이벤트 스키마, 등록, JSON 왕복, 편집기 보정, 런타임 효과 생성, 아이콘 |
| `RuntimeHost.cs` | 장면 전환을 감시하고 버튼/HUD 수명을 관리하는 단일 Unity 호스트 |
| `EditorGaugeButton.cs` | 에디터 활성화 버튼 생성·배치·동기화 |
| `MainGaugeHud.cs` | 체력 바·숫자·효과 문구와 상태색 |
| `GaugeBarGraphic.cs` | 모따기 게이지 메시와 색상 렌더링 |
| `PlanetGaugeSettings.cs` | UMM 직렬화 설정과 IMGUI |
| `PlanetGauge.csproj` | 게임/Unity/UMM 참조와 DLL·Info·아이콘 스테이징 |

## 4. 기본 체력 규칙

`GaugeRuntime.cs`가 단일 기준이다.

| `HitMargin` | 변화량 |
|---|---:|
| `Perfect` | `+0.1` |
| `EarlyPerfect` | `-0.5` |
| `LatePerfect` | `-0.5` |
| `VeryEarly` | `-1.5` |
| `VeryLate` | `-1.5` |
| `TooEarly` | `-3` |
| `FailMiss` | `-8` |
| `FailOverload` | `-8` |
| `TooLate`, `Multipress`, `Auto`, `OverPress`, 기타 | 직접 변화 없음 |

- 시작·기본 최대 체력은 `100`이다.
- 기본 회복은 100을 넘지 않는다.
- `TooLate`는 중간 상태라 직접 차감하지 않고 이후 확정되는 `FailMiss`만 반영한다.
- 기존 중복 차감 토큰과 복구 깊이 카운터는 유지된다.

## 5. 커스텀 이벤트 계약

### 식별자와 실행 시점

- 내부 이름: `SetPlanetGauge`
- 화면 이름: `PlanetGauge 설정`
- 숫자 enum ID: `0x5047` (`20551`)
- 카테고리: `Gameplay`
- 실행 시점: `OnBar`
- 첫 타일 허용: 예
- 같은 타일 중복 허용 여부는 바닐라 `LevelEventInfo` 기본 동작을 따른다.

### 속성 스키마

| 표시 이름 | 키 | 기본값 | 새 이벤트의 O/X | 동작 |
|---|---|---:|---|---|
| 속성 설정 | `attributeMode` | `Normal` | O | 아래 모드 중 하나 선택 |
| 증폭값 설정 | `multiplierPercent` | `100%` | X | O일 때만 저장된 증폭률을 교체 |
| 실패 방지 | `failureProtection` | 켜짐 | X | O일 때 현재 실패 방지 상태 교체 |
| 회복 상한 제한 | `recoveryCapEnabled` | 꺼짐 | X | O일 때 상한 제한 상태와 상한값 교체 |
| 회복 상한 | `recoveryCapPercent` | `100%` | 그룹 하위 | 상한 제한 값이 켜짐일 때만 표시 |
| 체력 상한 강제 제한 | `forceRecoveryCap` | 켜짐 | 별도 O/X 없음 | 상한 제한이 켜진 이벤트에서 즉시 체력을 상한까지 내릴지 결정 |

속성 모드:

| 모드 | 런타임 효과 |
|---|---|
| `Normal` | 원래 변화량 사용 |
| `BlockRecovery` / 회복 차단 | 양수 변화량을 0으로 변경 |
| `AmplifyIncrease` / 증가율 증폭 | 양수 변화량에 `증폭값 / 100` 곱하기 |
| `AmplifyDecrease` / 감소율 증폭 | 음수 변화량에 `증폭값 / 100` 곱하기 |
| `AmplifyBoth` / 증가·감소율 증폭 | 모든 변화량에 `증폭값 / 100` 곱하기 |

중요한 O/X 의미:

- O인 속성만 현재 런타임 설정을 바꾼다.
- X인 속성은 이전 타일에서 설정된 값을 보존한다.
- 증폭 모드를 사용자가 직접 고르면 `증폭값 설정` O/X는 바닐라 버튼 경로로 자동 O가 된다.
- 사용자가 이후 증폭값을 X로 바꾸면 기존 증폭값을 계속 사용한다.
- 실패 방지가 꺼져 있으면 `FailMiss`/`FailOverload`를 게이지로 흡수하지 않고 사망 처리한다. 단, 게임의 실제 no-fail은 우선한다.
- 회복 상한은 양수 회복에만 적용한다. 이미 상한보다 높은 체력은 강제 제한을 켠 이벤트가 실행될 때만 즉시 낮춘다.

### 수치 보정

- 증폭값: `NaN`/무한대 → `100`, 범위 `0..1000`.
- 회복 상한: `NaN`/무한대 → `100`, 범위 `0.1..100`.
- 편집기 입력 확정 시 `PropertyInfo.Validate(float)` Postfix에서 보정한다.
- 저장 직전 `LevelEvent.Encode(bool)` Prefix에서도 다시 보정한다.

## 6. 이벤트 등록과 JSON 왕복

`PlanetGaugeLevelEventRegistry`가 등록을 소유한다.

1. 모드 활성화 시 GCS 사전이 준비됐으면 즉시 등록한다.
2. 아직 준비되지 않았으면 활성화 자체는 성공시키고 `ADOStartup.SetupLevelEventsInfo` Postfix에서 등록한다.
3. 숫자 ID 충돌과 이름 충돌은 각각 예외로 보고한다.
4. `levelEventsInfo`에는 숫자 문자열 키 하나만 넣는다. 이름 키까지 넣으면 편집기 버튼이 중복된다.
5. `levelEventTypeString`에는 숫자 enum 값과 `SetPlanetGauge` 이름을 연결한다.

JSON 계약:

```json
{
  "eventType": "SetPlanetGauge"
}
```

- `RDUtils.ParseEnum<LevelEventType>` Prefix가 이름을 숫자 enum 값으로 바꾼다.
- `LevelEvent.Decode`는 같은 문자열을 enum 파싱과 `levelEventsInfo` 조회에 같이 사용하므로 Decode Prefix에서만 숫자 문자열 `20551`로 교체한다.
- `LevelEvent` 생성자 Prefix는 커스텀 `LevelEventInfo`를 주입한다.
- Encode Postfix는 정의되지 않은 enum의 숫자 `ToString()` 대신 다시 `SetPlanetGauge`를 저장한다.
- 현재 `ADOStartup.ModWasAdded("PlanetGauge")`를 호출한다. 별도 `LevelData.Encode` requiredMods 패치는 없으므로 배포 전 저장된 레벨의 의존성 표기는 실게임으로 확인한다.

## 7. 편집기 보정과 해결된 문제

### 초기화 전 등록 실패

과거에는 모드 활성화 시 `ADOStartup.SetupLevelEventsInfo`가 끝나지 않았으면 예외로 모드 전체가 꺼졌다. 현재는 준비 여부를 확인하고 지연 등록한다.

### 빈 인스펙터 패널

정의되지 않은 숫자 enum은 `Enum.GetValues`에 나오지 않는다. 바닐라 `InspectorPanel.ShowTabsForFloor`가 대체 이벤트를 고를 때 커스텀 이벤트만 남으면 `None`을 선택해 패널 본문이 비었다.

현재 Postfix는 다음 조건에서만 `ShowPanel(SetPlanetGauge, 0)`을 호출한다.

- 원본 처리 뒤 선택 타입이 `None`이고
- 해당 타일에 실제 `SetPlanetGauge` 이벤트가 하나 이상 있을 때

이 좁은 조건으로 이벤트 삭제, 다른 타일 전환, 바닐라 이벤트와의 전환에서 발생하던 빈 패널 문제를 해결했다. 사용자가 이후 실게임에서 정상이라고 확인했다.

### 회복 상한 표시 반전

`PropertyInfo.ValueMatch`의 Bool 조건 문자열은 대소문자를 구분하며 소문자 `"true"`를 요구한다. `bool.TrueString`의 `"True"`를 사용하면 조건이 반전된다. 회복 상한과 강제 제한의 `showIfVals`에는 반드시 소문자 리터럴을 유지한다.

### 런타임 효과 생성

`scnGame.ApplyEvent` Postfix는 바닐라가 처리하지 않아 `__result == null`인 `SetPlanetGauge`만 가로챈다. `PlanetGaugeLevelEventEffect : ffxPlusBase`를 대상 floor에 추가하고 `floorID`, `floors`, `crotchet`, `plusEffects`, 시작 시간, `sourceLevelEvent`를 구성한다. 중간 실패 시 추가한 컴포넌트와 리스트 항목을 롤백한다.

## 8. HUD 상태색과 효과 문구

일반 상태에서는 UMM에서 지정한 사용자 게이지 색을 사용한다. 이벤트 효과가 활성화되면 게이지 바와 숫자 텍스트에 같은 대표색을 적용한다.

| 효과 | 색상 | 문구 |
|---|---|---|
| 회복 차단 | `#B02020` | `Increase Disabled` |
| 증가율 증폭 | `#45D66B` | `Increase Amplified` |
| 감소율 증폭 | `#FF9F1C` | `Decrease Amplified` |
| 증가·감소율 증폭 | `#FFE36E` | `Rate Amplified` |
| 실패 방지 꺼짐 | `#2850A7` | `No-Fail Disabled` |
| 회복 상한 켜짐 | `#9B59D0` | `Increase Limited` |

- 여러 효과가 동시에 활성화되면 모든 문구를 숫자 위에 한 줄씩 표시하고 각 문구는 자기 색을 유지한다.
- 게이지/숫자는 한 색만 가질 수 있으므로 대표색 우선순위는 `속성 모드 → 회복 상한 → 실패 방지 꺼짐 → 사용자 색`이다.
- 색상 상수와 우선순위는 `MainGaugeHud.cs`에 주석으로 표시되어 있다.
- 상태나 사용자 색이 실제로 바뀔 때만 스타일과 TMP 문구를 다시 만든다. 레이아웃 계산은 게임 HUD 애니메이션을 따라 `LateUpdate`에서 수행한다.
- UI 크기별 2~3줄 동시 표시의 가독성은 계속 실게임 확인 대상이다.

## 9. 전용 이벤트 아이콘

- 원본: `PlanetGauge/Assets/Gaugeline.png`
- 현재 이미지: 300×300, 투명 ARGB PNG
- 배포 위치: `dist/PlanetGauge/Assets/Gaugeline.png`
- 런타임 경로: `UnityModManager.ModEntry.Path` 기준 `Assets/Gaugeline.png`
- Sprite 생성: 전체 이미지를 사용하고 128 PPU, 중앙 피벗
- PNG가 없거나 손상되면 `EventSettings`, 그다음 `SetSpeed`, 마지막으로 첫 가용 바닐라 아이콘을 빌린다.

설치본의 `UnityEngine.ImageConversionModule.dll`은 netstandard 2.1을 참조해 net48 프로젝트에서 직접 참조하면 `CS1705`가 발생한다. 따라서 선택 기능인 아이콘 로더만 확인된 `ImageConversion.LoadImage(Texture2D, byte[], bool)` 오버로드를 런타임 reflection으로 호출한다. API가 없으면 이벤트나 모드 전체를 실패시키지 않고 기본 아이콘으로 폴백한다.

커스텀 Sprite와 Texture는 캐시된다. 같은 게임 프로세스에서 PNG만 교체해도 즉시 다시 읽지 않으므로 파일 교체 후 게임 재시작을 권장한다.

## 10. 모드 수명과 등록 유지 정책

1. `Main.Load`가 `ModDirectory`, 설정, UMM 콜백만 등록한다.
2. Enable은 필수 API 검사 → Harmony 적용 → 상태 초기화 → 단일 `RuntimeHost` 생성 → 이벤트 등록 순서다.
3. 부분 실패 시 이 모드의 Harmony ID만 해제하고 호스트·상태·새로 추가한 이벤트 등록을 롤백한다.
4. 정상 Disable에서는 런타임 호스트와 패치를 제거하지만 이벤트 메타데이터는 현재 프로세스에 유지한다. 에디터와 이미 생성된 `LevelEvent`가 참조할 수 있기 때문이다.
5. `RuntimeHost.OnDestroy`는 버튼과 HUD를 제거하고 Unity 참조를 비운다.

세션 상태는 에디터 플레이 시작, 편집 모드 복귀, 재시작, 커스텀 레벨 리셋에서 `GaugeRuntime.Reset()`으로 초기화되며 이벤트 설정도 기본값으로 돌아간다.

## 11. 기존 판정/사망 흐름의 주의점

- `SwitchChosenPatch`는 원본 처리 전후 판정과 실패 바 상태를 결합한다.
- `TooLate`는 직접 차감하지 않는다.
- 확정 실패 뒤 이어지는 `Die`와의 중복 차감은 1회성 토큰으로 방지한다.
- 놓침 복구의 임시 no-fail은 깊이 카운터와 Postfix/Finalizer로 복원한다.
- 결과 문자열 생성 중에만 no-fail을 빌려 놓침/과부하 행을 만들고 즉시 원복한다.

해결한 과거 위험:

```text
SwitchChosen 원본 내부의 OnDamage(false/true, ...)
→ 연속 Multipress 9회 이상에서 Die(true, true, "", false)
→ PlayerDiePatch에서 FailOverload -8
→ 바깥 SwitchChosen Postfix가 실패 바를 다시 확인하면 추가 -8 가능
```

2026-08-19 설치본 IL에서 위 호출 계약을 확인했다. `SwitchChosenPatch`는 중첩 가능한 호출 깊이별 관찰 상태를 두고, 원본 호출 중 `PlayerDiePatch`가 실패 판정을 적용했으면 Postfix의 재적용을 건너뛴다. Postfix와 Finalizer가 관찰 상태를 멱등으로 종료한다. 연속 Multipress 9회 이상에서 체력이 `100 → 92`로 정확히 한 번만 감소하는지는 실게임으로 마지막 확인한다.

## 12. 빌드와 배포

```powershell
dotnet build PlanetGauge/PlanetGauge.csproj -c Release
```

게임 설치 위치가 기본값과 다르면 다음을 사용한다.

```powershell
dotnet build PlanetGauge/PlanetGauge.csproj -c Release `
  /p:GameManagedDir="<게임 Managed 폴더>"
```

빌드 타깃이 자동 갱신하는 항목:

```text
dist/PlanetGauge/PlanetGauge.dll
dist/PlanetGauge/Info.json
dist/PlanetGauge/Assets/Gaugeline.png
```

- 게임, Unity, Harmony, UMM DLL은 `Private=false`이며 배포물에 넣지 않는다.
- ZIP은 자동 생성하지 않는다. 릴리스 시 위 세 항목으로 새 ZIP을 만든다.
- 오래된 ZIP을 dist에 남기지 않는다. 사용자가 구버전을 설치하는 원인이 된다.
- 2026-08-19 최신 Release DLL은 52,736바이트, SHA-256 `2405C1FEC4CCA2AED2466028EA6A6BA0D0B92BA62C0F919EC68B99B6E9257085`이다. 기존 `dist/PlanetGauge/PlanetGauge.zip`은 스테이징 DLL보다 오래됐고 Info/DLL이 다르며 아이콘이 없어 제거했다. 릴리스 시 최신 3개 항목으로 새 ZIP을 만든다.
- `Info.json`, `AssemblyVersion`, `AssemblyFileVersion`은 모두 `0.1.0`이다.

## 13. 검증 현황

완료된 정적/빌드 검증:

- 현재 설치된 ADOFAI/Unity/UMM DLL 기준 Release 빌드 성공.
- 경고 0개, 오류 0개.
- 오래된 ZIP 제거 후 `Info.json`, `PlanetGauge.dll`, `Assets/Gaugeline.png` 3개만 남겨 엄격 패키지 검증 통과.
- 설치본의 `scrPlanet.SwitchChosen()` → `scrPlayer.OnDamage(bool,bool,bool,HitMargin)` → `scrPlayer.Die(bool,bool,string,bool)` Multipress 경로를 IL로 확인.
- 중복 차감 방지 관찰 상태를 reflection으로 검증: Die 없음 `false`, 중첩 내부 Die `true`/외부 `false`, 단일 Die `true`.
- 커스텀 이벤트 스키마를 reflection으로 생성해 `showIfVals`, O/X 여부를 확인.
- 일반 및 효과 조합의 HUD 대표색·문구 매핑을 reflection으로 확인.
- 원본/배포 아이콘 SHA-256 일치 확인.
- 배포 폴더에 게임/Unity/Harmony/UMM DLL이 없음을 확인.
- 사용자가 빈 인스펙터와 회복 상한 그룹 표시가 정상화됐다고 보고함.

빌드 성공은 실게임 안정성을 의미하지 않는다. 다음 수동 검증은 계속 유지한다.

1. 모드 enable/disable/re-enable에서 UI와 패치가 중복되지 않는가.
2. 이벤트 생성·복사·삭제·Undo·타일 이동·다른 이벤트 전환이 정상인가.
3. JSON 저장 → 재로드 → 재저장 시 `SetPlanetGauge`와 속성/O/X가 보존되는가.
4. 저장된 레벨의 requiredMods에 PlanetGauge 의존성이 기대대로 남는가.
5. 각 속성 모드가 양수/음수 판정에 정확히 적용되는가.
6. 증폭값 X가 이전 값을 보존하고 증폭 모드 직접 선택 시 O로 바뀌는가.
7. 실패 방지 OFF와 게임 실제 no-fail의 우선순위가 맞는가.
8. 회복 상한, 강제 상한, 0.1 최소값, NaN/무한대 보정이 맞는가.
9. 재시작·체크포인트·편집 복귀에서 체력과 이벤트 설정이 초기화되는가.
10. HUD 문구 1~3줄과 상태색이 여러 해상도/UI 배율에서 읽기 좋은가.
11. `Gaugeline.png`가 실제 이벤트 아이콘으로 보이고 누락/손상 시 폴백하는가.
12. PACL2/JALib 등 다른 모드와 함께 이벤트 패널 전환이 정상인가.
13. 연속 Multipress 9회 이상의 직접 사망 경로에서 체력이 `100 → 92`로 한 번만 감소하는가.

## 14. 다음 작업 후보

1. 위 수동 검증표, 특히 requiredMods·Multipress·PACL2 공존을 완료한다.
2. 아이콘의 실제 크기·여백을 확인하고 필요하면 128×128 PNG로 교체한다.
3. HUD 효과 3줄 동시 표시의 간격과 잘림을 확인한다.
4. 게이지 스킨 이벤트 속성은 현재 구조가 안정된 뒤 별도 설계한다.
5. 배포 전 새 ZIP을 만들고 DLL/Info/Assets 버전이 같은지 확인한다.

## 다음 채팅용 지시문

```text
PlanetGauge 개발을 이어서 진행해줘.
저장소 루트의 DEVELOPMENT_HANDOFF.md를 전부 읽고 git status와 현재 설치 DLL을 먼저 확인해.
현재 버전은 0.1.0이고 SetPlanetGauge 커스텀 이벤트, HUD 상태색/효과 문구, Gaugeline 아이콘까지 구현돼 있다.
기존 판정·사망 흐름과 커스텀 이벤트 직렬화 계약을 보존해.
수정 후에는 설치본 DLL 기준 Release 빌드와 관련 실게임 검증 항목을 구분해서 보고해.
```
