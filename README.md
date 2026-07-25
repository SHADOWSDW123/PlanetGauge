# PlanetGauge

얼불춤 레벨 에디터 시험 재생 전용 게이지 모드입니다. 일반 커스텀 레벨 선택이나 공식 레벨에는 관여하지 않습니다.

## 프로젝트 열기

Visual Studio에서 `PlanetGauge.slnx`를 열면 `PlanetGauge/PlanetGauge.csproj`가 연결됩니다.

프로젝트는 기본적으로 다음 Steam 설치 위치의 게임 DLL을 참조합니다.

```text
C:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice\
  A Dance of Fire and Ice_Data\Managed
```

설치 위치가 다르면 빌드할 때 경로를 지정합니다.

```powershell
dotnet build .\PlanetGauge.slnx -c Release `
  /p:GameManagedDir="D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed"
```

Unity Mod Manager DLL이 `Managed\UnityModManager` 이외의 위치에 있다면 `UnityModManagerDir`도 지정할 수 있습니다.

## 유지보수 지점

- 판정별 게이지 값: `PlanetGauge/GaugeRuntime.cs` 상단의 `*Delta` 상수
- UMM 진입점과 모드 활성화: `PlanetGauge/Main.cs`
- Harmony 패치: `PlanetGauge/Patches.cs`
- 에디터의 `PG` 토글 버튼: `PlanetGauge/EditorGaugeButton.cs`
- TMPro 디버그 표시: `PlanetGauge/GaugeDebugOverlay.cs`

게임 업데이트 뒤에는 먼저 `Assembly-CSharp.dll` 해시와 아래 패치 대상의 시그니처를 다시 확인해야 합니다.

- `scrPlanet.SwitchChosen`
- `scrPlayer.Die`
- `scnEditor.Play`
- `scrController.Restart`
- `scrController.ResetCustomLevel`

현재 구현 기준 DLL SHA-256:

```text
0C50DDAE9052612AA29D1BFF8878A006A23D8E6AC1105E0C61B78A8A4964D42B
```

## 빌드 결과

```powershell
dotnet build .\PlanetGauge.slnx -c Release
```

빌드가 끝나면 설치에 필요한 파일이 `dist\PlanetGauge`에 자동으로 정리됩니다.

- `PlanetGauge.dll`
- `Info.json`

다른 출력 위치가 필요하면 `/p:ModOutputDir="원하는 경로"`를 지정할 수 있습니다.
