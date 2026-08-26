# PlanetGauge 버전별 활동 및 검증 이력

최종 갱신: 2026-08-26. 이 문서는 기능 변경과 그 시점의 검증 결과를 기록한다. 현재 구조와 불변 계약은 [logics.md](logics.md)를 참조한다.

## 0.1.5C — 0.1.5A 기반 선별 안정화

0.1.5B의 광범위한 리팩터링을 그대로 병합하지 않고, 실제 위험이 명확한 변경만 0.1.5A 기준선에 다시 적용했다.

- 1,587줄의 `PlanetGaugeLevelEvent.cs`를 event types, registry, effects, serialization, editor, ApplyEvent 파일로 분리했다. 첫 분리 커밋은 타입 본문과 Harmony patch 수를 유지한 기계적 이동이다.
- 결과 화면과 실패 복구 중 임시로 변경하는 `scrController.noFail`의 실제 원래 값을 저장하고 Postfix/Finalizer에서 복원한다.
- reflection 대상인 `CheckPostHoldFail(ulong?)`와 `ParseEnum<T>(string, T) -> T`를 이름뿐 아니라 static 여부, 매개변수, 반환형까지 확인한다.
- `Main.ResetSessionState()`에서 게이지, 임시 no-fail depth, SwitchChosen 관찰 상태, 사망 보류 상태, 시각 전환을 함께 초기화한다.
- pause 상태의 Restart와 ResetCustomLevel도 세션 경계로 처리한다.
- custom event component를 만들기 전에 floor와 `bpm × pitch × speed`, angle offset을 검사한다. actual effect 실패는 list/component를 롤백하고 warning 실패는 actual effect를 유지한다.
- 정수 HUD에서 실제 체력이 `0`보다 크지만 반올림 결과가 `0`이면 `1`을 표시한다.
- 선택 기능인 에디터 아이콘 등록 실패는 기본 아이콘으로 저하하고 원본 에디터 로딩을 계속한다.
- `scrPlayer.Die()` 예외 직후 `FailAction()`을 연속 호출하지 않고 같은 세션의 반복 사망 진입을 막는다.
- 실제 HUD 표현 설정이 바뀔 때만 style revision을 증가시킨다.
- runtime command에서 소비하지 않던 warning offset/pulse 필드를 제거했다.

다음 B 변경은 C에 포함하지 않았다.

- `scrMarginTracker.AddHit` 판정 observer와 `SwitchJudgementObserver`
- sequence 기반 Die token
- `OptionalValue<T>` 및 command 타입 계층
- 임의의 warning 개수·offset 상한과 측정 없는 미세 최적화
- `PlanetGauge.Tests.exe` 콘솔 harness와 `GaugeStateKernel`

### 0.1.5C 검증 상태

- 현재 `Info.json = 0.1.5C` 상태에서도 Debug/Release 빌드는 경고 0개, 오류 0개다.
- 당시 숫자 버전 `0.1.5`/assembly `0.1.5.0` 기준 Strict 패키지 검사는 통과했다.
- 이후 `Info.json`은 별도 커밋에서 `0.1.5C`로 변경됐고 assembly는 `0.1.5.0`을 유지한다. 현재 Strict 패키지 검사는 이 버전 불일치로 실패한다.
- `dist/PlanetGauge/planetgauge_0.1.5c.zip`은 프로젝트가 선언한 staging 파일이 아니어서 Strict 검사 경고가 발생한다. 사용자 파일일 수 있으므로 자동 삭제하지 않았다.
- 현재 상태는 **build verified; package verification failed; in-game verification pending**이다.
- 활성화, enable/disable/re-enable, 판정 단일 반영, no-fail 복원, pause Restart, LevelEvent 저장 왕복, ForceRecovery warning, HUD `0.x` 표시는 인게임 확인 대상이다.

## 0.1.5 — ForceRecovery 사전 경고와 적용 전환

- 실제 ForceRecovery와 별도로 선행 경고를 같은 `ffxPlusBase` 게임 시간축에 등록했다. 경고만 `warningOffsetAngle`만큼 앞서고 실제 체력 변경·사망 호출 시점은 바꾸지 않는다.
- `warningOffsetAngle`(기본 `0°`, 양수·비유한수는 `0`)과 `warningPulseBeats`(기본 `0.5`, `0.125..16` 비트)를 추가했다. 둘 다 O/X가 없고 ForceRecovery에서만 보인다.
- 점멸 주기는 설치본 `Bloom.duration`처럼 `unit = "beats"`를 사용하며 한국어 설치본 표기는 `비트`다.
- 오프셋 `0`은 경고만 끈다. 네이티브 `angleOffset`은 실제 이벤트 시각 의미를 유지하며 에디터에는 노출하지 않았다.
- 경고는 최신 체력·상한으로 예상 범위를 매 프레임 갱신하고 검정↔차감 `#B02020`/회복 `#45D66B`을 beat 기반 삼각파로 표시한다.
- 실제 실행은 체력·숫자·사망을 즉시 확정한다. 실제 적용량만 `0.75초 OutCirc` 표시 오프셋으로 남기고 일반 판정은 이를 취소하지 않는다. 여러 전환은 합산되고 반대 방향은 상쇄된다.
- Blindfold 중에는 경고와 적용 전환을 숨긴다. 적용 전환은 `Time.unscaledDeltaTime`을 써 pause 중에도 끝난다.
- 경고 생성·등록·표시 전환 실패는 기록하되 ForceRecovery와 기존 사망 흐름은 계속 실행한다.
- 버전을 `0.1.5`/`0.1.5.0`으로 올렸고 설치본 게임 모드는 덮어쓰지 않았다.

### 0.1.5 완료 검증

- 설치본 `Assembly-CSharp.dll`/Harmony/UMM 참조 기준 Release 빌드 경고·오류 0개, `Info.json`/AssemblyVersion/FileVersion `0.1.5`/`0.1.5.0` 일치.
- Strict 패키지 검사 통과: dist에는 DLL, Info, Gaugeline만 있다.
- enum `Normal=0`~`ForceRecovery=6`, 이벤트 이름 `SetPlanetGauge`, ID `20551`, 새 경고 속성의 O/X·보정 계약을 빌드 어셈블리·소스에서 재확인했다.
- Release DLL: 72,704바이트, SHA-256 `DB878D179DBBDAE3FD14113ED46D7A7A96EA8DE6B3C0005093F552981EB6E646`.
- 과거 Debug DLL이 든 ZIP은 Strict 검사를 위해 제거했으며 새 ZIP은 아직 만들지 않았다. 게임은 실행하지 않았다.

## 0.1.4 — ForceRecovery, no-fail, Blindfold

- enum 끝에 `ForceRecovery=6`을 추가했다. 기존 `0..5` 값, 이벤트 이름/ID, 기존 JSON 키와 O/X 계약을 유지했다.
- `recoveryAmountPercent`(기본 `0`, `-1000..1000%`)만큼 현재 체력에 퍼센트포인트를 한 번 더하거나 뺀다. 회복 차단·증감률은 우회하지만 상한·사망·게임 no-fail 규칙은 따른다.
- ForceRecovery는 `attributeEnabled`를 숨기고 `attributeMode` O/X로 실행하며, `다른 속성 설정 끄기`는 계속 표시한다.
- PlanetGauge 활성 + `FailureProtection=false` + 게임 no-fail의 3중 조건에서 `FailMiss`/`FailOverload`가 나면 게이지만 0으로 동결하고 게임은 native no-fail로 계속 진행한다.
- Blindfold(`=5`) 중 바는 검정·숫자는 `???`다. 실제 사망/no-fail 0 이하 동결 시 숫자를 공개하고, 게이지가 실패를 흡수하면 숨긴다. 포털 착지 Prefix는 Blindfold만 끄고 다른 속성은 보존한다.
- 일반에서도 `다른 속성 설정 끄기`를 표시한다. 증감률 토큰 위치·효과 줄 간격을 정리하고 UMM 전체 게이지 크기 `25..200%`를 추가했다.
- 이벤트 태그 기능은 폐기했으며 구현하지 않았다.

### 0.1.4 완료 검증

- Release 빌드 경고·오류 0개, 버전 일치, Strict 패키지 검사 통과.
- reflection 계약 검사로 enum, 이벤트 이름/ID, `recoveryAmountPercent` 보정·에디터 노출, Blindfold 숨김/공개/클리어 해제를 확인했다.
- ForceRecovery Release: 66,560바이트, SHA-256 `BFC1C5E1538891820BBAF942E8539D70AFB3B093E47CA943C8CE917C1A3A5C68`.
- Blindfold Release: 65,024바이트, SHA-256 `333BFF0728FA57C08AC9181CFEED32F3A6BF2E681C6C49F341FBB2BC75B1BAD0`.
- 게임은 실행하지 않았다. 외부 reflection 환경은 `Mathf` 네이티브 호출을 실행할 수 없으므로 강제 회복·3중 Fail 런타임은 실게임 검증 대상이다.

## 0.1.2 — 속성 모델·Auto·디버그 HUD 기반

- 단일 속성 모드를 회복/피해 유효 채널과 독립 회복 차단으로 교체했다.
- Increase/Decrease/Both별 퍼센트를 기억하고 겹치는 부호는 마지막 명령만 유효하게 했다. 배율끼리는 곱하지 않는다.
- `attributeEnabled`, `disableOtherAttributes`, `autoTileRecovery`를 추가하면서 이벤트 ID·이름·기존 키·O/X를 유지했다.
- Reduced/Amplified 상태·출처색 토큰, `1000%` 상한, 수동/실제 Auto 회복, `0.5초 OutQuad` HUD 색 보간, Shift+F3 디버그 HUD를 추가했다.
- 설치본 DLL 기준 Release 빌드와 reflection 채널 계약 검사는 완료했지만 게임은 실행하지 않았다.

## 남은 실게임 검증

1. 이전 JSON과 새 키/O-X의 로드·저장·재로드, 복사·삭제·Undo/Redo.
2. 증감률 순서·범위, 상한·토큰·HUD, 수동/실제 Auto의 단일 회복과 패치 분리.
3. pause/restart/edit·해상도/UI 배율에서 HUD·디버그 표시 안정성.
4. no-fail/hitbox/연속 Multipress에서 실패 우선순위와 단일 차감.
5. Blindfold 숨김·사망 공개·클리어 해제와 3중 no-fail 동결.
6. ForceRecovery `+20/-20/0`의 단발 적용, 상한·사망·no-fail, 경고 키 보정·저장.
7. BPM 60/120/240/3120, 각 오프셋·주기의 경고 시작/위상, 최신 체력 예상 범위, 오버레이 합산·상쇄·Pause·세션 초기화.
8. 한국어 에디터의 점멸 주기 `비트` 단위 표기.

## 향후 개선점

- ForceRecovery는 일회성 명령인데 기존 호환성을 위해 `attributeMode` O/X를 실행 스위치로 재사용한다. 별도 command type/schema가 가능해지면 영속 속성과 분리하는 편이 낫다.
- 이벤트 태그 기능은 폐기된 요구이며 향후 작업 대상으로 간주하지 않는다.
