# Практическая работа №5 — Meta Quest 3 + сетевой бой (по методичке «Пр6»)

Основа: [ПР4](https://github.com/manhattanattack/pr4/tree/main). Реализация хранится **только в ветке `pr5`**. Сохранён режим ПК, добавлен автономный клиент Quest 3 (Android / OpenXR). Методичка из файла «ММ Пр5.docx» озаглавлена «Пр6»; выполнено её содержание для сдачи как ПР5.

## Что добавлено

| Пункт методички | Реализация |
| --- | --- |
| 1. Android / OpenXR / Quest 3 | `Packages/manifest.json`: XR Interaction Toolkit 3.x, XR Plug-in Management, OpenXR, OpenXR Meta. `Tools → PR5 → 2. Configure Android Quest 3` выставляет Android 10 (API 29), Auto target SDK, Vulkan, Linear, ASTC, IL2CPP, ARM64 и пытается включить Android OpenXR/Oculus Touch профили. |
| 2. `VR_LocalPlayer` | Редактор автоматически генерирует `Assets/Resources/VR_LocalPlayer.prefab` при открытии проекта. В нём: XR Origin, Camera Offset, Main Camera, Left Controller, Right Controller, CharacterController и визуальные сферы рук. Параметры контроллера 45°/0.3/0.05/0/0.3/1.8/центр (0,0.9,0). |
| 3. Физический клинок | `VR_Sword` внутри правого контроллера (позиция 0,0,0.25, поворот 45°, масштаб 0.05×0.5×0.05). Trigger Collider, kinematic Rigidbody без гравитации, `VRMeleeWeapon`. Событие взмаха при скорости не меньше 2.2 м/с и задержке 0.6 с. Урон по-прежнему считает сервер. |
| 4. Wrist HUD | `WristCanvas` в World Space на левом запястье: 300×200, scale 0.0006, позиция 0/0.06/-0.05, поворот 60°. Показ HP, миникарты и 2D-маркеров через `VRWristHUD`; экранный боевой HUD отключается при VR. |
| 5. `VRPlayerController` | Позиции 6DoF от OpenXR, левый стик движение 4 м/с, правый стик snap-turn 45°, правый триггер стреляет из кисти, haptic impulse (0.7/0.15 меч; 0.5/0.1 выстрел), телепорт/респавн. В Unity 6 используется XR Interaction Toolkit 3.x: вместо устаревших `ActionBasedController` и `Locomotion System` из 2.x реализовано аналогичное поведение через XR Input + `CharacterController`. |
| 6. Сетевой аватар 6DoF | `NetworkPlayer` получает положение тела, головы, двух рук и ориентации; сглаживает интерполяцией с коэффициентом 15. Редактор добавляет `LeftHand_Mesh` и `RightHand_Mesh` в `NetworkPlayer.prefab` при первом открытии Unity. На ПК видны движения VR-участника. |
| 7. Клиент `MOVE_VR` | `NetworkClient` каждые 50 мс отправляет позы Quest, считывает их из `SNAPSHOT`, оставляет старый `MOVE` для ПК. Отправляет `ATTACK_MELEE` и `FIRE_RANGED`. |
| 8. Сервер / уклонения / шум | `vrMovement.js` валидирует позиции и скорости; сервер рассылает состояние `MOVE_VR`, при резком движении генерирует шум, `TankMob.js` проверяет высоту головы менее 1.1 м и сообщает `ATTACK_DODGED` без потери HP. |
| 9. APK + парный тест | Исходники готовы для Android-сборки в Unity; APK требует локального Unity Editor с Android Build Support и реального Quest 3. Инструкция ниже. |

## Первый запуск в Unity 6000.4.6f1

1. Откройте репозиторий в Unity Hub именно на ветке `pr5`. Дождитесь установки новых UPM-пакетов и завершения компиляции.
2. После импорта автоматически создаются `Assets/Resources/VR_LocalPlayer.prefab`, `Assets/Resources/QuestConnectionSettings.asset`, а в `NetworkPlayer.prefab` появляются маркеры обеих рук. Для ручного повторения: меню **Tools → PR5 → 1. Create VR prefab and Quest settings**.
3. Запустите **Tools → PR5 → 2. Configure Android Quest 3 (OpenXR/Vulkan/ARM64)**.
4. Откройте **Edit → Project Settings → XR Plug-in Management → Android** и убедитесь, что выбран **OpenXR**, а в OpenXR / Interaction Profiles включены **Oculus Touch Controller Profile** и **Meta Quest Touch Pro Controller Profile**. При необходимости добавьте их через «+»; проверьте **Project Validation**. Приоритет OpenXR, не Oculus XR Plugin.
5. Откройте созданный **Assets/Resources/QuestConnectionSettings.asset** и **обязательно замените пример** `192.168.1.42` на LAN IPv4 ПК с Node.js сервером. Укажите `port = 7777` и `nickname`. В APK `127.0.0.1` относится к шлему и не подключит его к компьютеру.
6. Через **File → Build Profiles** выберите **Android**, Compression **ASTC**, переключитесь на Android. Проверьте Player: Vulkan ONLY, Linear, IL2CPP, ARM64, Minimum API 29.
7. Запустите сервер на ПК: `cd Assets/MazeServer && npm ci && npm start` (либо `node server.js`). Quest и компьютер должны быть в одной Wi-Fi/LAN-сети, TCP/7777 должен быть доступен через firewall.
8. Разрешите Developer Mode / USB Debugging для Quest 3, подключите USB-C, выберите шлем в **Run Device** и нажмите **Build And Run**. Сохраните APK как `MultiplayerVR_Quest3.apk`. Лобби в Android автоматически подключится к IP из `QuestConnectionSettings`. На ПК используйте обычное меню подключения и адрес того же сервера.

## Проверка в паре

1. **Quest 3:** физический наклон головы, положения обеих рук и локомоция работают; на левом запястье видны HP и карта.
2. **ПК:** второй игрок видит в реальном времени голову и две кисти VR-участника; игра продолжает отправлять `MOVE`.
3. **Меч:** медленный жест не вызывает атаку; резкий вызывает серверную проверку урона и вибрацию.
4. **Танк:** при замахе 0.8 сек VR-игрок пригибается так, чтобы высота шлема была ниже 1.1 м; сервер сообщает `ATTACK_DODGED`, HP сохраняется.
5. **Слепой моб:** тихое движение оставляет его без новой цели; быстрый взмах (≥2.2 м/с) слышен, ускорение движения слышно на меньшей дистанции.

## Проверка сервера

```bash
cd Assets/MazeServer
npm ci
npm test
node --check server.js
node --check TankMob.js
node server.js
```

`tests/vrMovement.test.js` проверяет валидацию VR-пакетов, интерполяционные поля, тихое движение/шум и правило уворота. Есть GitHub Actions workflow `PR5 VR server tests` для ветки `pr5`.

**Ограничения проверки:** Unity Editor, Android SDK, физический Quest 3 и работа сетевой пары здесь не запускались, поэтому не утверждается, что APK уже собран или все пять сценариев проверены на устройстве. Сгенерированные при первом импорте `.prefab`/`.asset` стоит сохранить и закоммитить после открытия Unity, если нужно зафиксировать их в GitHub. Префаб VR также создаётся в рантайме как запасной путь, если `Resources/VR_LocalPlayer` отсутствует.

## Режим ПК

На десктопе проект продолжает использовать прежние `LocalPlayer.prefab`, ввод WASD/мышь, левую и правую кнопки мыши, лобби, миникарту, мобов и лут. Новые VR-функции включаются только в Android (или по флагу `simulateVRInEditor` у `NetworkClient`).
