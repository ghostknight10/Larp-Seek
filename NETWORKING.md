# Networking Design & Decision Record — Larp-Seek

## Project Environment
- **Unity Version**: `6000.3.26f1`
- **Cloud Project**: `3b5e4a77-eca6-4746-a5d6-7da02d3a6cbe` (Organization: `ayaan_sharma`)
- **Active Scene**: `Assets/mess.unity` (Build Index `0`)
- **Packages (`Packages/packages-lock.json`)**:
  - `com.unity.netcode.gameobjects`: `2.14.0`
  - `com.unity.services.multiplayer`: `2.5.0`
  - `com.unity.services.core`: `1.19.0`
  - `com.unity.services.authentication`: `3.9.0`
  - `com.unity.transport`: `2.7.4`
  - `com.unity.multiplayer.playmode`: `2.0.2`

## Architecture & Topology
- **Topology**: Client-hosted Listen Server using **Netcode for GameObjects (NGO)** over **Unity Transport** + **Unity Multiplayer Services (Sessions + Relay)** for online join-by-code lobbies.
- **Why**: Small casual friends-only Prop Hunt game (`maxPlayers` default `8`). Listen-server avoids dedicated server hosting costs, and Unity Services (`MultiplayerService`) bundles Lobby + Relay allocation into a single short shareable Join Code (`ISession.Code`) that handles NAT traversal for free under UGS free tiers.
- **Primary Startup Path (Online Code)**:
  - Host initializes `UnityServices` + anonymous `AuthenticationService` sign-in, calls `MultiplayerService.Instance.CreateSessionAsync(options.WithRelayNetwork())`, displays `session.Code` in Lobby UI.
  - Client initializes + signs in anonymously, enters the code in Join modal, and calls `MultiplayerService.Instance.JoinSessionByCodeAsync(code)`.
  - Multiplayer Services' `NetworkManagerSession` handler configures `UnityTransport` with Relay data and starts `NetworkManager.StartHost()` / `NetworkManager.StartClient()` automatically (never double-started).
- **Local Direct Fallback (LAN / Verification)**:
  - Local `StartHost()` / `StartClient()` over direct `UnityTransport` (`127.0.0.1:7777` or LAN IP) for offline testing or local MPPM/Standalone verification.

## Authority & Replicated State
- **Player Movement**: Owner-authoritative `NetworkTransform` (`AuthorityMode.Owner`) driven by `FirstPersonRoam` (`CharacterController`), giving zero local input latency in a casual Prop Hunt game while replicating position and yaw rotation to all clients.
- **Replicated Player Variables (`NetworkPlayerController`)**:
  - `NetworkVariable<FixedString64Bytes> PlayerName` (Read: Everyone, Write: Server via `SetPlayerNameRpc`).
  - `NetworkVariable<int> MorphedPropIndex` (Read: Everyone, Write: Server via `RequestMorphRpc`, `-1` = human avatar, `0..N-1` = deterministic index into `PropRegistry`).
- **Replicated Match State (`NetworkMatchState`)**:
  - `NetworkVariable<bool> MatchStarted` (Read: Everyone, Write: Server) transitions all lobby participants from Lobby Room into active hide-and-morph gameplay simultaneously.
- **Deterministic Prop Sync (`PropRegistry`)**:
  - Collects all `PropTarget` components in the scene and sorts them deterministically by transform hierarchy path so every peer maps `MorphedPropIndex` to the exact same prop geometry.

## Parity Accounting
- `FirstPersonRoam` and `PropMorphController` remain the single source of truth for movement speeds, jump/gravity, hover raycasting, `CharacterController` resizing, and `MorphedProp` instantiation across solo, host, and client processes.
- Remote player objects disable their local `Camera`, `AudioListener`, and input handlers cleanly on non-owner instances while keeping `PlayerVisualRoot` synchronized.

## Verification Log
- **Build Verification**:
  - `StandaloneWindows64` build (`Temp/TestBuild/LarpSeek.exe`, `Development` mode, `Assets/mess.unity` at Build Index `0`) completed with `Result=Succeeded`, `Errors=0`.
- **Single-Process UI & Parity Verification (`Main Editor` Play Mode)**:
  - Verified `EventSystem` (`Count = 1`, `InputSystemUIInputModule = true`) and `27` active Canvas `RectTransform` elements (`ZeroSize = []`).
  - Visually inspected Game View captures (`Temp/UIReview/main_menu_v2.png`, `join_modal_v2.png`, `lobby_room.png`, `hud.png`, `pause_menu_v2.png`, `online_lobby_host.png`). Fixed card overflow clipping using `ContentSizeFitter (VerticalFit = PreferredSize)` on the menu/modal cards.
- **Unity Cloud Services & Join Code Check (`Main Editor` + `LarpSeek.exe`)**:
  - Verified `UnityServices.InitializeAsync()` + `AuthenticationService.SignInAnonymouslyAsync()` + `MultiplayerService.Instance.CreateSessionAsync(options.WithRelayNetwork())` succeeded against linked project `3b5e4a77-eca6-4746-a5d6-7da02d3a6cbe`, issuing live Join Codes (`BD9NRD`, `FRRHGD`) displayed in the Lobby UI.
  - On the current campus NAT network (`172.16.131.42`), outbound DTLS UDP traffic to Relay timed out (`Failed to establish connection with the Relay server`); local LAN Direct IP mode works unaffected and is verified cross-process below.
- **Two-Process Cross-Executable Gameplay Verification (`Main Editor` Host port `7800` + `LarpSeek.exe` Client port `7900`)**:
  - Host started LAN (`127.0.0.1:7777`) and Client joined via `JoinLanClient("127.0.0.1", 7777, "Friend_Exe")`.
  - Both processes converged on `LobbyCount = 2`, `PropRegCount = 1377`, and spawned `NetworkPlayer(Clone)` instances at `(-56.50, -3.24, -41.18)` and `(-56.50, -3.24, -42.53)`.
  - Host started match (`StartMatchFromLobby()`): both peers transitioned simultaneously to `InMatch = True`.
  - Host moved & morphed into `Water Dispenser` (`(-55.90, -3.18, -42.13)`); Client moved & morphed into `Dustbin` (`(-54.70, -3.18, -40.38)`). Both processes reported identical `Players(2)` positions and morph names, visually confirmed in `Temp/UIReview/multiplayer_active.png`.
  - Client requested revert (`OnLocalRevertRequested`): both Host and Client immediately reported Client back to `morphed = False, morphName = Human` while Host stayed `Water Dispenser`.
  - Console errors after test cycle on both Host (`7800`) and Client (`7900`): `0`.
