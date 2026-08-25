# PlanetGauge 개발 인수인계

최종 갱신: 2026-08-25

이 문서는 **현재 작업 상태와 다음 작업의 진입점**만 담는다. 기능별 동작과 호환성 계약은 [logics.md](logics.md), 버전별 변경·검증 이력은 [updates.md](updates.md)를 확인한다.

## 현재 스냅샷

| 항목 | 값 |
|---|---|
| 브랜치 | `main` (`origin/main`과 동일한 `86e8bcc`, 0.1.5는 미커밋 작업 트리) |
| 작업 시작 HEAD | `86e8bcc` (`Update README.md`) |
| 현재 소스 버전 | `0.1.5` |
| 타깃 | .NET Framework 4.8 / C# 7.3 |
| 전용 이벤트 | `SetPlanetGauge`, `0x5047` (`20551`) |
| 최신 Release | 경고 0개, 오류 0개 |
| Release DLL | 72,704바이트, SHA-256 `DB878D179DBBDAE3FD14113ED46D7A7A96EA8DE6B3C0005093F552981EB6E646` |

이 갱신 시점에는 0.1.5 구현이 아직 커밋되지 않은 작업 트리에 있다. 작업 전 반드시 다음을 확인한다.

```powershell
git status --short --branch
git diff --check
git diff
```

## 먼저 읽을 문서

- [logics.md](logics.md): 실패 우선순위, 이벤트 JSON/O-X 호환성, 런타임 상태, HUD·디버그 구조, 설치 DLL API 기준선.
- [updates.md](updates.md): 0.1.2~0.1.5 변경사항, 빌드 산출물, 완료 검증과 남은 실게임 검증.
- [README.md](README.md): 사용자용 설치·기능 안내.

## 다음 작업의 안전 규칙

- `SetPlanetGauge` 이름/ID `20551`, 기존 JSON 키, `LevelEvent.disabled` 기반 O/X 해석을 보존한다.
- 기존 판정·사망 흐름을 유지한다. 실패 우선순위는 `게임 실제 no-fail > PlanetGauge 체력 > 원본 사망`이다.
- ForceRecovery의 선행 경고와 0.75초 적용 오버레이는 **시각 전용**이다. 실제 체력·숫자·사망 확정 시각을 바꾸면 안 된다.
- Release 빌드·정적 검증과 실게임 검증 결과를 구분해 기록한다.

## 다음 채팅용 지시문

```text
PlanetGauge 0.1.5 작업을 이어서 진행해줘.
저장소 루트 DEVELOPMENT_HANDOFF.md, logics.md, updates.md를 읽고 git status와 현재 설치 DLL을 먼저 확인해.
기존 판정·사망 흐름과 SetPlanetGauge 이름/ID/기존 JSON 키/O-X 계약을 보존해.
ForceRecovery 선행 경고와 0.75초 표시 전환은 시각 전용으로 유지해.
Release 빌드 검증과 아직 필요한 실게임 검증을 구분해서 보고해.
```
