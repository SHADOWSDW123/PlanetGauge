# PlanetGauge C# 소스 감사 및 개선 필요 항목

감사 기준일: 2026-08-25  
대상: 현재 작업 트리의 `PlanetGauge/` C# 소스 12개, 생성된 `obj/` 파일 제외 총 5,090줄  
범위 밖: README·핸드오프·에셋 내용·게임 직접 실행  
상태: 정적 분석, 설치본 `Assembly-CSharp.dll` API 대조, Debug/Release 빌드 및 패키지 검증 완료. **실게임 검증은 하지 않았다.**

## 점수 기준

- **관리 필수도**: 앞으로 기능을 더 얹기 전에 정리해야 하는 정도. `0`은 관리 불필요, `10`은 추가 개발 전에 반드시 해결해야 함.
- **중요도**: 방치했을 때 사용자 동작·데이터·게임 안정성·다른 모드 공존에 주는 영향. `0`은 무시 가능, `10`은 모드의 핵심 신뢰성을 무너뜨림.
- **확실성**: `확정`은 코드만으로 문제가 성립함, `높음`은 발생 조건만 런타임에 의존함, `검증 필요`는 실게임 재현으로 최종 판단해야 함.
- **난이도**: `S`는 국소 수정, `M`은 여러 호출부/계약 수정, `L`은 상태 모델이나 패치 흐름 재설계.

점수는 “코드가 보기 싫은 정도”가 아니라 **PlanetGauge를 계속 개발할 때 실제로 관리해야 할 우선순위**를 나타낸다.

## 요약

| ID | 요약 | 관리 필수도 | 중요도 | 확실성 |
|---|---|---:|---:|---|
| NF-01 | pause 상태의 Restart/Reset에서 세션 초기화가 누락될 수 있음 | 10 | 9 | 검증 필요 |
| NF-02 | 게임 판정 로직을 Prefix에서 다시 계산해 바닐라·타 모드와 어긋날 수 있음 | 10 | 9 | 높음 |
| NF-03 | enable/disable 롤백 중 예외가 나면 패치·객체가 고아로 남을 수 있음 | 9 | 8 | 확정 |
| NF-04 | 늦은 커스텀 이벤트 등록 실패 후 모드가 정상 활성 상태로 남음 | 9 | 9 | 확정 |
| NF-05 | 커스텀 효과 생성 트랜잭션이 실제 커밋보다 일찍 끝남 | 8 | 8 | 높음 |
| NF-06 | 핵심 상태 머신과 직렬화 계약에 자동화 테스트가 전혀 없음 | 10 | 8 | 확정 |
| NF-07 | HUD·에디터 UI 생성이 중간 실패에 안전하지 않음 | 8 | 7 | 확정 |
| NF-08 | 세션 상태가 여러 static 소유자에 분산되고 Reset 범위가 불완전함 | 9 | 7 | 확정 |
| NF-09 | 임시 no-fail 복원이 원래 값을 저장하지 않아 다른 패치의 변경을 덮을 수 있음 | 8 | 7 | 높음 |
| NF-10 | 다음 Die용 1회성 토큰에 사건 identity와 모든 종료 경로 정리가 없음 | 8 | 8 | 높음 |
| NF-11 | `Die` 예외를 삼킨 뒤 부분 실행 상태에서 `FailAction`을 재호출함 | 7 | 8 | 확정 |
| NF-12 | reflection 대상 선택이 매개변수형·반환형을 충분히 검증하지 않음 | 8 | 7 | 확정 |
| NF-13 | 같은 타일의 복수 PlanetGauge 이벤트에서 항상 index 0 패널을 선택함 | 7 | 7 | 검증 필요 |
| NF-14 | 경고 오프셋과 동시 경고 수에 하한·상한이 없어 프레임 비용이 무제한임 | 7 | 7 | 높음 |
| NF-15 | 커스텀 효과 시간 계산에서 0·NaN·Infinity를 방어하지 않음 | 7 | 8 | 높음 |
| NF-16 | 기본 HUD가 살아 있는 소수 체력을 `0`으로 표시할 수 있음 | 8 | 7 | 확정 |
| NF-17 | 선택 기능인 아이콘 등록 실패가 에디터 원본 메서드를 깨뜨릴 수 있음 | 6 | 7 | 높음 |
| NF-18 | 판정 오차 미터를 끄면 핵심 체력 HUD도 사라짐 | 7 | 6 | 확정/의도 확인 |
| NF-19 | 강제 회복 실제 계산과 미리보기 계산이 복제되어 있음 | 7 | 6 | 확정 |
| NF-20 | 이벤트 명령·설정이 Bool 플래그 묶음과 긴 위치 인자 생성자에 의존함 | 7 | 5 | 확정 |
| NF-21 | `PlanetGaugeLevelEvent.cs` 한 파일에 서로 다른 책임이 과도하게 집중됨 | 8 | 5 | 확정 |
| NF-22 | 런타임 명령에 읽히지 않는 경고 필드가 남아 있음 | 5 | 2 | 확정 |
| NF-23 | 매 프레임 sibling 순서 변경과 반복 `GetComponent`가 발생함 | 6 | 4 | 확정 |
| NF-24 | 메시 재생성 때 불필요한 배열 할당이 발생함 | 4 | 3 | 확정 |
| NF-25 | 사용자 UI 언어가 한국어와 영어 하드코딩으로 혼재함 | 6 | 5 | 확정 |
| NF-26 | 판정값·누계 인덱스 매핑이 별도 switch로 중복됨 | 6 | 5 | 확정 |
| NF-27 | 모든 이벤트가 스타일 revision을 올려 무관한 색 전환도 재시작시킴 | 4 | 3 | 확정 |

## 즉시 신뢰성 개선 대상

### NF-01. pause 상태 Restart/Reset의 게이지 초기화 누락 가능성

- **평가**: 관리 필수도 `10/10`, 중요도 `9/10`, 확실성 `검증 필요`
- **위치**: `PlanetGauge/Patches.cs:31-55`, `PlanetGauge/GaugeRuntime.cs:168-185`
- **근거**: `ControllerRestartPatch`와 `ResetCustomLevelPatch`는 `GaugeRuntime.ShouldHandle()`이 true일 때만 `Reset()`한다. 그런데 `ShouldHandle()`은 `controller.paused`면 false다. Restart는 일반적으로 pause 메뉴에서 시작될 수 있으므로, 호출 Prefix 시점에 pause가 아직 풀리지 않았다면 이전 체력·frozen·Blindfold·Die 토큰·경고가 다음 시도에 남는다.
- **왜 존재하는가**: 일반 판정 패치가 pause 중 동작하지 않도록 만든 공통 gate를 세션 경계 패치에도 재사용한 것으로 보인다.
- **권장 방향**: 세션 경계에는 판정용 `ShouldHandle()`을 쓰지 말고 `IsGameplayContext(true)`에 준하는 별도 `ShouldResetSession()`을 둔다. Restart/Reset의 실제 호출 시점에서 paused 값을 로그로 확인하고, pause 메뉴·즉시 재시작·체크포인트 재시작을 각각 검증한다.
- **난이도/회귀 위험**: `S~M` / 중간. 공식 레벨이나 멀티플레이까지 잘못 초기화하지 않도록 에디터 1P 범위는 유지해야 한다.

### NF-02. 바닐라 판정 로직의 부분 재구현

- **평가**: 관리 필수도 `10/10`, 중요도 `9/10`, 확실성 `높음`
- **위치**: `PlanetGauge/Patches.cs:95-158`, 특히 `142-149`
- **근거**: `SwitchChosen` Prefix가 cached angle, target angle, CW, BPM, pitch, marginScale을 모아 `scrMisc.GetHitMargin()`을 직접 다시 호출한다. 이는 원본 `SwitchChosen`의 판정 결정 전체를 관찰하는 것이 아니라 현재 알고 있는 입력으로 **예측 판정**을 만드는 방식이다. 게임 업데이트, 특수 타일, 다른 모드 Prefix, 원본 내부의 추가 보정이 생기면 게임 통계에 기록된 판정과 PlanetGauge가 차감한 판정이 달라질 수 있다.
- **왜 존재하는가**: 원본 실행 후 행성·바닥 상태가 바뀌므로 Prefix에서 판정 입력을 미리 고정하려는 목적은 타당하다.
- **권장 방향**: 설치 DLL에서 판정이 최종 확정되는 가장 좁은 지점을 다시 찾는다. 가능한 순서는 실제 margin tracker/grade 변화 관찰, 판정을 받는 작은 메서드 패치, 마지막 수단으로 현재 계산을 한 곳에 격리하고 바닐라 결과와 진단 비교하는 방식이다. 현재 계산을 유지한다면 지원 게임 DLL 해시와 특수 분기를 명시한 계약 테스트가 필수다.
- **난이도/회귀 위험**: `L` / 매우 높음. 사망 중복 방지와 Auto 분기를 함께 다시 검증해야 한다.

### NF-03. enable/disable 정리가 예외 안전하지 않음

- **평가**: 관리 필수도 `9/10`, 중요도 `8/10`, 확실성 `확정`
- **위치**: `PlanetGauge/Main.cs:83-138`, `198-223`
- **근거**: enable 실패 catch는 `GaugeRuntime.Reset → RuntimeHost.DestroyHost → UnpatchAll → registry rollback`을 순서대로 호출하지만, 각 정리 단계 자체를 보호하는 `try/finally`가 없다. `DestroyHost`나 `UnpatchAll`이 예외를 던지면 뒤 정리가 생략된다. Disable도 먼저 `IsEnabled=false`로 바꾼 뒤 같은 방식으로 진행하므로, 중간 실패 시 다음 Disable은 조기 반환하고 고아 Harmony 패치를 제거하지 못한다. 다음 Enable은 `harmony` 참조를 덮어써 고아 패치를 추적할 수도 없다.
- **왜 존재하는가**: 정상 경로에서 Unity `Destroy`와 Harmony unpatch가 실패하지 않는다는 전제 아래 간결하게 작성됐다.
- **권장 방향**: 정리를 각각 시도하는 멱등 `CleanupActivation(bool rollbackMetadata)`로 통합하고, 개별 실패를 수집·로그하면서도 마지막 단계까지 실행한다. Harmony ID는 참조가 없어도 `modEntry.Info.Id`로 정리할 수 있게 보존한다. 활성화 상태는 모든 필수 단계 성공 후에만 최종 확정한다.
- **난이도/회귀 위험**: `M` / 중간.

### NF-04. 늦은 이벤트 등록 실패가 활성화 실패로 승격되지 않음

- **평가**: 관리 필수도 `9/10`, 중요도 `9/10`, 확실성 `확정`
- **위치**: `PlanetGauge/PlanetGaugeLevelEvent.cs:1262-1292`, `PlanetGauge/Main.cs:104-119`
- **근거**: Enable 시 게임 registry가 아직 준비되지 않으면 정상적으로 대기한다. 이후 `SetupLevelEventsInfo` Postfix에서 ID 충돌이나 registry 이상으로 `TryRegister()`가 실패하면 예외를 로그만 남기고 모드는 `IsEnabled=true` 상태를 유지한다. 사용자는 토글과 HUD는 보지만 핵심 `SetPlanetGauge` 이벤트를 만들거나 해석할 수 없는 반쪽 활성 상태가 된다.
- **왜 존재하는가**: 게임 시작 Postfix에서 예외를 전파해 전체 게임 초기화를 깨뜨리지 않으려는 안전 장치다. 예외를 게임으로 전파하지 않는 판단은 맞다.
- **권장 방향**: 예외는 삼키되 모드 내부 상태를 `CoreIntegrationFailed`로 전환하고 런타임 기능·에디터 버튼을 중지한다. UMM에 명확한 오류를 노출하고 재등록/재활성화 경로를 제공한다. “아이콘만 실패”와 “이벤트 identity 충돌”을 구분해야 한다.
- **난이도/회귀 위험**: `M` / 중간.

### NF-05. 커스텀 효과 생성의 트랜잭션 범위가 불완전함

- **평가**: 관리 필수도 `8/10`, 중요도 `8/10`, 확실성 `높음`
- **위치**: `PlanetGauge/PlanetGaugeLevelEvent.cs:1480-1576`
- **근거**: 실제 effect는 `1500-1512`에서 생성·목록 추가·`__result` 지정 후 첫 try/catch를 빠져나온다. 그 뒤 `1529-1541`의 mode/속성 읽기에서 손상된 JSON 변환 등이 예외를 내면 effect는 이미 floor에 남고 Postfix 예외가 레벨 로딩으로 전파된다. 즉 “실제 effect 생성 + 선택적 warning 생성”이라는 하나의 작업이 완전히 커밋되기 전에 rollback 범위를 종료한다.
- **왜 존재하는가**: warning은 선택 시각 기능이므로 실패해도 실제 이벤트는 유지하려고 두 단계로 나눈 것이다.
- **권장 방향**: warning 필요 여부와 모든 입력 보정을 Unity 객체 생성 전에 계산한다. 실제 effect 커밋 뒤에는 예외를 낼 수 있는 작업을 두지 않는다. 실제 effect 트랜잭션과 warning의 독립적인 best-effort 트랜잭션을 명시적으로 나눈다.
- **난이도/회귀 위험**: `M` / 중간.

### NF-06. 자동화된 회귀 테스트 부재

- **평가**: 관리 필수도 `10/10`, 중요도 `8/10`, 확실성 `확정`
- **위치**: 저장소에 테스트 프로젝트·테스트 C# 파일·`InternalsVisibleTo`가 없음. 핵심 대상은 `GaugeRuntime.cs`, `Patches.cs`, `PlanetGaugeLevelEvent.cs`.
- **근거**: 이 모드는 마지막 채널 우선순위, no-fail 3중 조건, 중복 Die 토큰, 복구 depth, ForceRecovery 상한/사망, JSON O/X, NaN/Infinity 보정이라는 회귀하기 쉬운 상태 머신을 가진다. 현재 검증은 컴파일과 임시 reflection 계약에 의존하며, 실제 상태 전이 결과를 반복 검증할 장치가 없다.
- **왜 존재하는가**: Unity/ADOFAI static singleton과 `Mathf` 의존 때문에 일반 테스트 호스트에서 실행하기 어렵고, 기능을 빠르게 추가하며 수동 검증에 의존한 것으로 보인다.
- **권장 방향**: Unity와 무관한 순수 `GaugeState` 전이 커널을 먼저 분리하고 표 기반 테스트를 만든다. 최소 행렬은 채널 순서, cap, block, no-fail, ForceRecovery, frozen, pending Die consume/clear다. JSON은 설치 DLL을 참조하는 별도 계약 테스트로 `JSON → LevelEvent → JSON`과 O/X를 검증한다.
- **난이도/회귀 위험**: `L` / 초기에는 높음, 완료 후 전체 회귀 위험을 크게 낮춤.

## 높은 우선순위 구조·공존 문제

### NF-07. UI 생성이 중간 실패 후 자기 복구하지 못함

- **평가**: 관리 필수도 `8/10`, 중요도 `7/10`, 확실성 `확정`
- **위치**: `PlanetGauge/MainGaugeHud.cs:125-180`, `PlanetGauge/EditorGaugeButton.cs:32-78`, `PlanetGauge/GaugeDebugHud.cs:63-106`
- **근거**: 세 생성 경로 모두 root/static owner를 먼저 기록한 뒤 여러 컴포넌트와 자식을 만든다. 중간 예외가 나면 부분 root가 남는다. 특히 MainGaugeHud는 다음 프레임 `sourceMeter == meter && rootObject != null` 조건으로 생성 완료로 오인하고 조기 반환한 뒤 null text를 사용해 매 프레임 예외를 낼 수 있다. Editor 버튼도 `owner == editor && buttonObject != null`만으로 완료를 판단한다.
- **왜 존재하는가**: Unity 컴포넌트 생성은 정상 환경에서 성공한다는 전제와 단순한 중복 방지 때문이다.
- **권장 방향**: 지역 변수로 전부 생성한 뒤 필드에 커밋하고, catch에서 root를 파괴·필드를 초기화한다. 완료 조건은 필요한 모든 참조를 확인하거나 명시적인 `created` 상태로 관리한다. Update 최상단에서 반복 예외가 로그를 폭주시킬 수 있으므로 한 번의 실패 후 안전한 비활성 상태를 둔다.
- **난이도/회귀 위험**: `M` / 낮음~중간.

### NF-08. session state의 소유권과 Reset 경계가 분산됨

- **평가**: 관리 필수도 `9/10`, 중요도 `7/10`, 확실성 `확정`
- **위치**: `Main.cs:16-18`, `GaugeRuntime.cs:20-28`, `Patches.cs:81-82`, `GaugeVisualTransitions.cs:47-48`, `PlanetGaugeLevelEvent.cs:222-227`, `EditorGaugeButton.cs:22-29`
- **근거**: 실패 복구 상태가 `Main.temporaryMissRecoveryDepth`, `GaugeRuntime.failureRecoveryDepth/nextDieAlreadyCharged`, `SwitchChosenPatch.observedSwitchDepth/배열`에 나뉘어 있다. `GaugeRuntime.Reset()`은 자기 상태와 visual transition만 초기화하며 Main·SwitchChosen의 관찰 상태는 건드리지 않는다. 정상 Finalizer가 항상 실행되면 정리되지만 toggle·예외·세션 경계의 방어 계약은 한눈에 증명할 수 없다.
- **왜 존재하는가**: 각 Harmony 패치가 필요해질 때 가장 가까운 클래스에 국소 상태를 추가한 결과다. depth와 one-shot token 자체는 ADOFAI 중복 이벤트를 처리하는 올바른 기법이다.
- **권장 방향**: gameplay session 상태 소유자를 하나로 만들거나 최소한 `ResetSessionState()`가 모든 패치 상태를 명시적으로 호출하도록 한다. registry/icon처럼 process-lifetime 상태와 gameplay-session 상태를 타입·파일 수준에서 분리한다.
- **난이도/회귀 위험**: `L` / 높음.

### NF-09. 임시 no-fail 복원이 원본 값을 캡처하지 않음

- **평가**: 관리 필수도 `8/10`, 중요도 `7/10`, 확실성 `높음`
- **위치**: `Main.cs:295-318`, `380-413`, `457-481`, `Patches.cs:347-370`
- **근거**: Prefix 진입 시 현재 값이 false/true임을 검사하지만 `__state`에는 원본 Bool을 저장하지 않고 복구 시 각각 false 또는 true를 하드코딩한다. 원본 메서드나 더 낮은 우선순위의 다른 모드 패치가 호출 중 `noFail`을 합법적으로 변경하면 PlanetGauge Finalizer가 그 변경을 덮는다.
- **왜 존재하는가**: 단독 실행에서는 진입 조건이 원본 값을 이미 보장하므로 필드를 하나 줄일 수 있다.
- **권장 방향**: 모든 임시 flag scope가 `OriginalNoFail`을 캡처해 그대로 복원하도록 통일한다. 중첩 호출에서 같은 controller를 공유할 때 depth별 원본 값이 보존되는지 테스트한다.
- **난이도/회귀 위험**: `S~M` / 중간.

### NF-10. pending Die 토큰의 수명과 identity가 약함

- **평가**: 관리 필수도 `8/10`, 중요도 `8/10`, 확실성 `높음`
- **위치**: `GaugeRuntime.cs:21`, `378-385`; `Patches.cs:178-214`, `294-323`
- **근거**: `nextDieAlreadyCharged`는 사건·player·switch depth와 연결되지 않은 전역 Bool이다. Fail 판정 후 예상한 Die가 오지 않거나, 다음 Die가 hitbox/auto/controller-null 경로로 조기 반환하면 토큰을 소비하지 않는 경로가 있다. 이후 다른 Die가 토큰을 소비해 실제 차감을 생략할 수 있다. 지원 범위가 1P라 player identity 문제는 줄지만 사건 수명 문제는 남는다.
- **왜 존재하는가**: `SwitchChosen`과 뒤따르는 `Die`가 같은 논리 실패를 보고하는 것을 시간창 없이 막기 위한 1회성 토큰이며, 기법 자체는 적절하다.
- **권장 방향**: 토큰을 관찰 중인 switch/failure sequence와 묶고, hitbox·auto·controller null·세션 종료 등 모든 exit에서 명시적으로 폐기한다. “다음 Die가 반드시 온다”는 설치 DLL 제어 흐름을 테스트로 고정한다.
- **난이도/회귀 위험**: `M` / 높음.

### NF-11. 사망 예외를 삼킨 뒤 대체 사망을 실행함

- **평가**: 관리 필수도 `7/10`, 중요도 `8/10`, 확실성 `확정`
- **위치**: `PlanetGauge/GaugeRuntime.cs:412-437`
- **근거**: 직접 호출한 `player.Die()`가 예외를 내면 로그 후 `controller.FailAction()`을 호출하고 원래 예외를 외부로 전달하지 않는다. `Die()`가 일부 상태를 이미 변경한 뒤 예외를 냈다면 `FailAction()`이 같은 사망 흐름을 두 번째로 시작할 수 있다. 둘 다 실패해도 체력은 frozen 0인 채 호출자는 성공처럼 반환받는다.
- **왜 존재하는가**: 게이지 소진 시 어떤 경우에도 플레이를 실패 상태로 보내려는 최후 폴백이다.
- **권장 방향**: 설치 DLL에서 `Die`의 실패 원자성을 증명할 수 없다면 부분 실행 뒤 다른 고수준 진입점을 호출하지 않는다. 최소한 fatal 상태를 기록하고 기능을 정지하며, 진단 빌드에서는 원 예외를 보존한다. 폴백을 유지하려면 호출 전후 상태를 검사해 아직 사망이 시작되지 않은 경우에만 실행한다.
- **난이도/회귀 위험**: `M` / 높음.

### NF-12. reflection 대상 선택이 과소 지정됨

- **평가**: 관리 필수도 `8/10`, 중요도 `7/10`, 확실성 `확정`
- **위치**: `PlanetGauge/Main.cs:337-345`, `487-503`; `PlanetGauge/PlanetGaugeLevelEvent.cs:554-570`
- **근거**: `FindMethodByName()`은 이름이 같은 첫 메서드를 반환하고 매개변수·반환형·static 여부를 확인하지 않는다. `FindParseEnumMethod()`도 generic 인자 수와 parameter 개수만 확인하며 실제 `(string, T) → T` 계약을 검사하지 않는다. 설치본에는 현재 각각 `CheckPostHoldFail(ulong?)`, `ParseEnum<T>(string,T)` 하나만 있어 동작하지만, 오버로드 추가 시 조용히 잘못된 메서드를 고르거나 PatchAll을 실패시킬 수 있다.
- **왜 존재하는가**: private/제네릭 API를 버전 차이 속에서 찾기 위한 호환성 코드다.
- **권장 방향**: 설치본에서 확인한 정확한 parameter type과 반환형을 검증한다. `CheckPostHoldFail`은 `Nullable<ulong>`, ParseEnum은 첫 인자 string·둘째 generic T·반환 T를 명시한다. 선택 API라면 누락과 모호성을 별도로 로그한다.
- **난이도/회귀 위험**: `S` / 낮음.

### NF-13. 복수 커스텀 이벤트 inspector 선택이 index 0으로 고정됨

- **평가**: 관리 필수도 `7/10`, 중요도 `7/10`, 확실성 `검증 필요`
- **위치**: `PlanetGauge/PlanetGaugeLevelEvent.cs:1313-1340`
- **근거**: 바닐라 선택이 `None`일 때 해당 floor에 PlanetGauge 이벤트가 하나 이상 있는지만 확인한 뒤 `ShowPanel(EventType, 0)`을 호출한다. 같은 타일에 SetPlanetGauge가 여러 개 있거나 native/custom 혼합 선택 상태가 있을 때 사용자가 의도한 occurrence가 아닌 첫 이벤트를 열 수 있다.
- **왜 존재하는가**: undefined enum이 `Enum.GetValues`에 없어 패널이 비는 문제를 가장 좁은 Postfix로 복구하려는 코드다. 이 우회 자체는 스킬 기준에 부합한다.
- **권장 방향**: 설치본 `ShowTabsForFloor`의 eventIndex 계산 계약을 디컴파일해 실제 선택 occurrence를 보존한다. 최소한 복수 이벤트 create/copy/delete/Undo/Redo/타일 이동 행렬을 실게임 검증한다.
- **난이도/회귀 위험**: `M` / 중간.

### NF-14. 경고 오프셋·동시 overlay가 사실상 무제한

- **평가**: 관리 필수도 `7/10`, 중요도 `7/10`, 확실성 `높음`
- **위치**: `PlanetGauge/PlanetGaugeLevelEvent.cs:154-162`, `679-690`; `GaugeVisualTransitions.cs:47-48`, `76-105`, `155-191`; `GaugeBarGraphic.cs:189-197`
- **근거**: `warningOffsetAngle`은 양수만 0으로 고치며 유한한 음수에는 하한이 없다. 매우 큰 음수와 많은 ForceRecovery 이벤트를 가진 맵은 장시간 다수 warning을 활성화할 수 있다. 매 프레임 모든 warning을 순회해 preview·색·segment를 만들고, 메시에도 warning당 꼭짓점과 삼각형을 추가한다. 리스트·프레임 비용·메시 크기에 상한이 없다.
- **왜 존재하는가**: 제작자가 임의 각도만큼 일찍 경고하도록 표현력을 제한하지 않은 설계다.
- **권장 방향**: 의미 있는 최대 선행 각도/시간을 정하거나, 화면에는 가까운 N개 또는 합성된 구간만 표시한다. 맵 로드 시 이벤트 수와 offset을 canonicalize하고 초과 시 한 번만 경고한다.
- **난이도/회귀 위험**: `M` / 중간.

### NF-15. 효과 시간 계산 입력의 비유한수·0 방어 누락

- **평가**: 관리 필수도 `7/10`, 중요도 `8/10`, 확실성 `높음`
- **위치**: `PlanetGauge/PlanetGaugeLevelEvent.cs:1496-1511`, `1548-1559`
- **근거**: `crotchet = 60 / (bpm * pitch * floor.speed)`는 분모 0·NaN·Infinity를 검사하지 않는다. 공통 `angleOffset`과 `offset`도 sanitization 없이 `SetStartTime`에 전달한다. 정상 게임 데이터에서는 안전할 수 있지만 손상·수동 편집 JSON이나 향후 API 변화에서 무한 시간/NaN effect가 `plusEffects`에 들어갈 수 있다.
- **왜 존재하는가**: 해당 값들이 바닐라 레벨 로더에서 이미 유효하다는 전제를 따른다.
- **권장 방향**: 커스텀 effect 경계에서 분모가 유한하고 양수인지 검증한다. 실제 effect는 core이므로 invalid timing이면 문맥을 포함해 레벨 적용을 명확히 실패시키고, warning만 잘못된 경우에는 warning만 비활성화한다.
- **난이도/회귀 위험**: `S~M` / 낮음.

### NF-16. 기본 정수 HUD가 실제 생존 상태와 다른 숫자를 표시함

- **평가**: 관리 필수도 `8/10`, 중요도 `7/10`, 확실성 `확정`
- **위치**: `PlanetGauge/MainGaugeHud.cs:641-661`, `PlanetGauge/PlanetGaugeSettings.cs:18`
- **근거**: 소수 표시 기본값은 false이고 정수 표시는 `Mathf.RoundToInt(displayValue)`다. 따라서 실제 체력 `0.1`은 `0`으로 보이지만 캐릭터는 살아 있고, `99.6`은 `100`으로 보인다. 특히 0 표시는 사망 여부를 잘못 전달한다.
- **왜 존재하는가**: 숫자를 간결하게 보여주기 위해 일반 반올림을 사용했다.
- **권장 방향**: 생존 체력은 최소 1로 표시하거나, 피해 기반 체력 HUD에서는 floor/ceil 중 명시적인 정책을 정한다. 가장 정확한 방법은 소수 표시를 기본으로 하거나 `0 < Current < 1`만 별도 형식으로 보여주는 것이다.
- **난이도/회귀 위험**: `S` / 낮음.

### NF-17. 선택 기능인 아이콘 경로가 에디터 Prefix에서 보호되지 않음

- **평가**: 관리 필수도 `6/10`, 중요도 `7/10`, 확실성 `높음`
- **위치**: `PlanetGauge/PlanetGaugeLevelEvent.cs:342-373`, `1296-1305`
- **근거**: PNG 읽기·Sprite 생성은 내부에서 fallback하지만 `EnsureIcon()` 자체의 dictionary 접근·할당은 `LoadEditorProperties` Prefix에서 try/catch 없이 호출된다. registry가 재생성 중이거나 다른 모드가 비표준 dictionary 상태를 만들면 선택 기능인 아이콘 실패가 원본 에디터 속성 로딩까지 중단시킬 수 있다.
- **왜 존재하는가**: 정상 registry에 대한 단순 idempotent 보정이며, 이미지 로드의 일반 실패는 이미 처리한다.
- **권장 방향**: Prefix 전체를 optional integration 경계로 감싸고 한 번만 경고한 뒤 원본을 계속 실행한다. icon dictionary 충돌은 기존 값을 존중하고 에디터 기능과 분리한다.
- **난이도/회귀 위험**: `S` / 낮음.

## 유지보수성과 완성도

### NF-18. 판정 오차 미터 OFF가 PlanetGauge HUD OFF를 강제함

- **평가**: 관리 필수도 `7/10`, 중요도 `6/10`, 확실성 `확정/의도 확인`
- **위치**: `PlanetGauge/MainGaugeHud.cs:676-715`, 특히 `694-698`
- **근거**: `Persistence.hitErrorMeterSize == Off`이면 체력 HUD를 숨긴다. PlanetGauge의 핵심 정보가 별개 사용자 설정에 종속되며, 오차 미터를 싫어하는 사용자는 게이지도 볼 수 없다.
- **왜 존재하는가**: 오차 미터를 위치·스케일 기준점으로 사용하고 관련 UI가 숨으면 모드 UI도 존중하려는 설계다.
- **권장 방향**: 제품 의도를 먼저 확정한다. 게이지가 독립 기능이라면 meter OFF에서 검증된 Canvas/컨트롤러 anchor fallback을 사용한다. 의도적으로 종속한다면 UMM/README에서 명확히 알리고 코드에는 이유를 계약으로 남긴다.
- **난이도/회귀 위험**: `M` / 중간~높음(UI 배율 검증 필요).

### NF-19. ForceRecovery 실제값과 preview 계산 중복

- **평가**: 관리 필수도 `7/10`, 중요도 `6/10`, 확실성 `확정`
- **위치**: `PlanetGauge/GaugeRuntime.cs:299-348`, `351-375`
- **근거**: amount 보정, frozen/0 처리, 상한 적용, no-fail `-5` 바닥, 일반 사망 0 제한이 `ApplyForcedRecovery()`와 `PreviewForcedRecovery()`에 거의 동일하게 두 번 구현돼 있다. 한쪽만 수정되면 사전 경고가 실제 적용 범위와 달라진다.
- **왜 존재하는가**: preview가 상태를 바꾸지 않아야 하므로 빠르게 복제한 것으로 보인다.
- **권장 방향**: 입력 state와 amount에서 `ForcedRecoveryOutcome { Next, ShouldDie, ActualDelta }`를 계산하는 순수 함수 하나를 두고 preview와 commit이 같은 결과를 사용한다.
- **난이도/회귀 위험**: `M` / 중간. 테스트를 먼저 추가해야 한다.

### NF-20. EventCommand 플래그 묶음과 긴 settings 생성자

- **평가**: 관리 필수도 `7/10`, 중요도 `5/10`, 확실성 `확정`
- **위치**: `PlanetGauge/PlanetGaugeLevelEvent.cs:54-134`, `GaugeRuntime.cs:68-146`
- **근거**: `PlanetGaugeEventCommand`는 `ApplyX` Bool과 값 17개를 가진 mutable struct이며 서로 모순되는 조합을 만들 수 있다. `PlanetGaugeEventSettings`는 Bool·float·channel 11개를 위치 인자로 받는다. `ApplyEventSettings`는 모든 값을 지역 변수로 복사한 뒤 다시 조립하므로 필드 추가 때 복사·생성자·기본값 중 하나를 빠뜨리기 쉽다.
- **왜 존재하는가**: 한 LevelEvent가 여러 optional O/X 속성을 동시에 변경하는 직렬화 계약을 한 객체로 전달하기 위함이다.
- **권장 방향**: JSON decode DTO와 유효한 runtime command를 분리한다. 속성, cap, failure, auto, ForceRecovery처럼 현재 책임 단위의 명시적 명령으로 나누거나 최소한 named factory와 `With...` 메서드로 invalid combination 생성을 막는다. speculative interface 계층은 만들 필요가 없다.
- **난이도/회귀 위험**: `L` / 높음.

### NF-21. `PlanetGaugeLevelEvent.cs`의 과도한 책임 집중

- **평가**: 관리 필수도 `8/10`, 중요도 `5/10`, 확실성 `확정`
- **위치**: `PlanetGauge/PlanetGaugeLevelEvent.cs` 전체 1,587줄
- **근거**: 한 파일이 enum/domain struct, 값 보정, registry 수명, PNG reflection loader, API 검증, PropertyInfo schema, 실제/경고 ffx, ParseEnum/constructor/decode/encode/float/editor-selection/setup/icon/inspector/localization/apply-event Harmony 패치를 모두 가진다. 변경 이유와 수명(scope)이 다른 코드가 함께 있어 리뷰 시 영향 범위를 파악하기 어렵다.
- **왜 존재하는가**: `SetPlanetGauge` 기능을 한 파일에 모아 외부 파일 수를 줄이려 한 것으로 보인다. 클래스 자체는 이미 나뉘어 있어 런타임 결합이 반드시 큰 것은 아니다.
- **권장 방향**: 새 추상화 없이 파일만 책임별로 분리한다. 예: `EventContract.cs`, `EventRegistry.cs`, `EventSchema.cs`, `EventSerializationPatches.cs`, `EventEditorPatches.cs`, `EventEffects.cs`, `EventIcon.cs`. `Main.cs` 안의 gameplay Harmony 패치도 `Patches.cs` 또는 별도 lifecycle patch 파일로 옮긴다.
- **난이도/회귀 위험**: `S~M` / 낮음(기계적 이동으로 제한할 경우).

### NF-22. 읽히지 않는 warning command 필드

- **평가**: 관리 필수도 `5/10`, 중요도 `2/10`, 확실성 `확정`
- **위치**: `PlanetGauge/PlanetGaugeLevelEvent.cs:125-126`, `850-853`, `888-889`
- **근거**: `WarningOffsetAngle`과 `WarningPulseBeats`를 실제 effect의 `PlanetGaugeEventCommand`에 저장하지만 `GaugeRuntime.ApplyEventSettings()`를 포함한 어떤 소비자도 읽지 않는다. warning effect는 같은 LevelEvent에서 값을 별도로 다시 Decode한다.
- **왜 존재하는가**: 0.1.5 기능을 기존 command decode 경로에 일단 포함했거나, 향후 runtime에서 쓸 가능성을 남긴 흔적으로 보인다.
- **권장 방향**: command에서 두 필드와 불필요한 decode를 제거한다. 미래 용도를 이유로 dead field를 유지하지 않는다.
- **난이도/회귀 위험**: `S` / 매우 낮음.

### NF-23. UI hot path의 hierarchy 변경과 반복 컴포넌트 조회

- **평가**: 관리 필수도 `6/10`, 중요도 `4/10`, 확실성 `확정`
- **위치**: `PlanetGauge/MainGaugeHud.cs:125-155`, `676-710`; `EditorGaugeButton.cs:89-106`
- **근거**: HUD가 정상 생성된 뒤에도 매 LateUpdate마다 `SetAsLastSibling()`을 호출한다. 다른 모드도 같은 전략을 쓰면 서로 sibling 순서를 매 프레임 갱신하며 Canvas rebuild와 공존 경쟁을 만들 수 있다. straight/curved meter와 실패 방지 버튼의 `GetComponent<RectTransform>()`도 hot path에서 반복된다.
- **왜 존재하는가**: 다른 HUD가 런타임에 추가돼 게이지를 가리는 상황과 동적 meter 전환을 즉시 복구하려는 방어다.
- **권장 방향**: owner/parent/sibling count 또는 source object 변경 시에만 순서를 갱신한다. straight/curved RectTransform과 shield RectTransform은 owner identity가 바뀔 때 캐시한다.
- **난이도/회귀 위험**: `S~M` / 낮음.

### NF-24. Gauge 메시 생성 중 배열 할당

- **평가**: 관리 필수도 `4/10`, 중요도 `3/10`, 확실성 `확정`
- **위치**: `PlanetGauge/GaugeBarGraphic.cs:315-340`
- **근거**: `AddChamferedRect()`가 호출될 때마다 8개 `Vector2[]`를 새로 만든다. 색 전환 중에는 매 프레임 `SetVerticesDirty()`가 발생하므로 작은 GC allocation이 반복된다.
- **왜 존재하는가**: 읽기 쉬운 지역 점 배열로 팔각형을 구성했다.
- **권장 방향**: 고정 8점을 개별 지역 변수로 추가하거나 인스턴스 버퍼를 재사용한다. 프로파일링에서 GC가 의미 없으면 낮은 우선순위로 둔다.
- **난이도/회귀 위험**: `S` / 낮음.

### NF-25. 사용자 UI 문자열의 언어·현지화 정책 불일치

- **평가**: 관리 필수도 `6/10`, 중요도 `5/10`, 확실성 `확정`
- **위치**: `PlanetGauge/PlanetGaugeSettings.cs:34-127`, `EditorGaugeButton.cs:135-139`, `MainGaugeHud.cs:481-527`, `PlanetGaugeLevelEvent.cs:1372-1446`
- **근거**: 에디터 property/localization은 한국어, UMM 설정·게이지 상태·알림은 영어로 하드코딩돼 있다. 현재 언어를 기준으로 선택하는 한 곳의 정책이 없고 문자열이 여러 파일에 흩어져 있다.
- **왜 존재하는가**: 기능별 구현 시점과 UI 표면이 달라 각 위치에서 직접 문자열을 넣었다.
- **권장 방향**: 지원 언어 범위를 정한다. 한국어 전용이어도 한 문자열 테이블로 모으고, 다국어를 지원한다면 RDString/UMM 언어 기준 adapter 한 곳에서 반환한다. 범용 localization framework까지 만들 필요는 없다.
- **난이도/회귀 위험**: `M` / 낮음.

### NF-26. judgement delta와 total index 매핑 중복

- **평가**: 관리 필수도 `6/10`, 중요도 `5/10`, 확실성 `확정`
- **위치**: `PlanetGauge/GaugeRuntime.cs:441-480`, 상단 delta 상수 `8-18`
- **근거**: 지원 HitMargin 목록을 `TryGetDelta()`와 `GetTotalIndex()` 두 switch에서 따로 유지하고 total 배열 크기 `8`도 별도 상수 없이 박혀 있다. 판정을 추가·삭제하거나 순서를 바꿀 때 누계만 빠지는 식의 조용한 오류가 가능하다.
- **왜 존재하는가**: 판정 계산과 디버그 누계를 빠른 array/switch로 구현했다.
- **권장 방향**: 하나의 정적 descriptor table 또는 명시적 `JudgementDefinition` 목록을 source of truth로 사용한다. 성능이 걱정되면 작은 switch 하나가 delta와 index를 함께 반환해도 충분하다.
- **난이도/회귀 위험**: `S~M` / 낮음.

### NF-27. style revision 범위가 실제 스타일 변경보다 넓음

- **평가**: 관리 필수도 `4/10`, 중요도 `3/10`, 확실성 `확정`
- **위치**: `PlanetGauge/GaugeRuntime.cs:137-146`, `MainGaugeHud.cs:286-310`
- **근거**: `ApplyEventSettings()`는 ForceRecovery처럼 HUD 색 상태를 바꾸지 않는 이벤트도 항상 `styleRevision++`한다. 진행 중인 색 전환은 현재색에서 같은 target으로 다시 0.5초 시작될 수 있고 effect/rate 문자열도 불필요하게 재생성된다.
- **왜 존재하는가**: 변경 필드를 비교하지 않고 안전하게 HUD cache를 무효화하려는 단순한 방식이다.
- **권장 방향**: 이전·새 설정의 HUD 표현 관련 필드만 비교하거나 `styleRevision`과 `valueRevision`을 분리한다. 실제 문제가 관찰되지 않으면 NF-23 이후 처리한다.
- **난이도/회귀 위험**: `S` / 낮음.

## 코드 냄새로 기록하되 즉시 재설계하지 않을 항목

- `Main.cs`가 UMM lifecycle과 세 개의 gameplay Harmony patch를 함께 가진다. NF-21 파일 분리 때 정리하면 충분하다.
- `CreateEventInfo()`는 길고 string 기반 schema가 많지만 `PropertyInfo`의 네이티브 계약을 표현하므로 일반적인 builder framework를 추가하면 오히려 과설계가 된다. 현재 helper `CreateProperty`, `MakeOptional`, `AddProperty` 정도가 적절하다.
- `GaugeBarGraphic.cs`는 464줄로 크지만 현재 editor의 rounded gradient bar와 main HUD의 chamfer/overlay라는 실제 두 소비자를 가진다. 단순히 길다는 이유로 삭제할 대상은 아니다. 메시 성능만 NF-24처럼 측정 가능한 부분부터 줄인다.
- `GaugeDebugHud`는 배포 코드에 포함된 진단 UI지만 Shift+F3라는 명시적 현재 기능과 실제 상태 관찰 목적이 있다. 숨겨진 dead debug code는 아니다. 다만 테스트가 생기면 출력 문자열과 런타임 상태 조회를 분리할 수 있다.
- `MaximumGauge`와 `InitialGauge`가 현재 둘 다 100인 것은 중복처럼 보이나 “초기값”과 “기본 정규화 상한” 의미가 달라 제거 우선순위는 낮다.

## 문제에서 제외한 의도된 ADOFAI 호환성 우회

다음 코드는 일반 C# 프로젝트에서는 이상해 보이지만 `develop-adofai-mod` 스킬과 설치 DLL 계약에 부합하므로 그 자체를 결함으로 보지 않았다.

- 정의되지 않은 `LevelEventType` 숫자 `0x5047/20551`을 쓰고 JSON에는 `SetPlanetGauge`를 저장하는 identity 모델.
- `ParseEnum`, LevelEvent constructor/Decode/Encode를 좁게 패치해 숫자 enum과 사람이 읽을 수 있는 문자열을 왕복시키는 구조.
- custom event 선택 때 바닐라 `ShowTabsForFloor`를 먼저 실행하고 `None`일 때만 보완하는 Postfix. 단, occurrence index 문제는 NF-13으로 별도 기록했다.
- 에디터 dictionary에 숫자 키와 문자열 키를 동시에 넣지 않는 처리.
- normal disable에서 LevelEvent metadata를 process lifetime 동안 유지하는 정책. 기존 LevelEvent와 inspector가 metadata를 참조할 수 있기 때문이다.
- net48과 설치본 ImageConversionModule의 참조 충돌을 피하기 위한 정확한 `LoadImage(Texture2D, byte[], bool)` reflection. icon은 optional이며 실패 시 native icon으로 degrade한다.
- `ffxPlusBase` 실제 effect와 선행 warning effect를 분리하고, warning 실패가 실제 event를 막지 않게 하는 방향.
- failure recovery의 depth counter, SwitchChosen/Die 중복을 막는 one-shot token, Postfix+Finalizer 복원 패턴. **기법은 올바르지만 현재 reset/identity/원본 값 복원 구현은 NF-08~10의 대상이다.**
- `DontDestroyOnLoad` RuntimeHost. Update/LateUpdate, Shift+F3 입력, scene UI 재획득이 실제 필요하므로 host 존재 자체는 과설계가 아니다.
- custom ParseEnum과 localization Prefix가 PlanetGauge의 정확한 key에서만 원본을 건너뛰는 처리.

## 검증 근거

### 설치 DLL에서 확인한 정확한 API

- `scrPlayer.CheckPostHoldFail(Nullable<ulong>)`
- `RDUtils.ParseEnum<T>(string, T) → T`
- `scrPlanet SwitchChosen()`
- `scrPlayer.Die(bool overload, bool multipress, string failMessage, bool hitbox)`
- `scrController.Restart(bool)`
- `scrController.ResetCustomLevel(bool) → IEnumerator`
- `scrController.OnLandOnPortal(scrPlanet, Portal, string)`
- `scnGame.ApplyEvent(LevelEvent, float, float, List<scrFloor>, float, int?)`
- `InspectorPanel.ShowPanel(LevelEventType, int)`
- `ffxPlusBase.SetStartTime(float, float)`

### 빌드·패키지

- `dotnet build PlanetGauge/PlanetGauge.csproj -c Debug`: 경고 0, 오류 0.
- `dotnet build PlanetGauge/PlanetGauge.csproj -c Release`: 경고 0, 오류 0.
- SDK analyzer `AllEnabledByDefault`: 경고 0, 오류 0.
- Strict 패키지 검사: 통과. DLL/Info/Gaugeline 존재, 버전 `0.1.5` ↔ assembly `0.1.5.0` 일치.
- 빈 catch, 전역 Harmony unpatch, TODO/FIXME placeholder, 패키지 내 게임/Harmony/UMM DLL은 발견하지 못했다.

빌드 성공은 NF-01, NF-02, NF-04, NF-09, NF-10, NF-13처럼 Unity 런타임 순서와 다른 모드 공존에 의존하는 문제를 증명하거나 반증하지 않는다.

## 권장 처리 순서와 의존 관계

1. **테스트 가능성 확보**: NF-06을 시작하면서 현재 상태 전이 golden matrix를 먼저 고정한다.
2. **세션·사망 신뢰성**: NF-01, NF-08, NF-09, NF-10, NF-11을 한 묶음으로 수정한다. 서로 같은 no-fail/Die/session 계약을 건드리므로 따로 배포하면 회귀 위험이 크다.
3. **판정 source of truth**: NF-02를 별도 브랜치 수준으로 다루고 판정·Auto·Multipress·no-fail 실게임 행렬을 수행한다.
4. **활성화·이벤트 원자성**: NF-03, NF-04, NF-05, NF-12, NF-15, NF-17을 처리한다.
5. **HUD 정확성·수명**: NF-07, NF-16, NF-18을 처리한 뒤 다양한 해상도/UI 배율을 검증한다.
6. **구조 정리**: 동작 테스트가 생긴 뒤 NF-19~NF-22, NF-26을 리팩터링한다.
7. **성능·표현 마감**: NF-14, NF-23~NF-25, NF-27을 프로파일링과 제품 언어 결정에 맞춰 처리한다.

## 결론

현재 코드는 **빌드와 패키지 계약은 양호하고, ADOFAI 특수 호환성 우회도 대체로 이유가 분명하다.** 불필요한 범용 abstraction이나 service container 같은 전형적인 과설계는 없다. 문제의 중심은 반대쪽에 가깝다. 기능이 빠르게 누적되면서 session/no-fail/Die 상태가 여러 static owner로 흩어졌고, 이 복잡도를 지켜줄 자동화 테스트가 없다.

따라서 새 기능을 바로 추가하기보다 NF-01~NF-12, 특히 **세션 reset·판정 source of truth·실패 복구 상태·테스트 기반**을 먼저 다루는 것이 모드 신뢰도와 향후 개발 속도에 가장 큰 효과가 있다.
