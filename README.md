# Практическая работа №3 — PvP в Unity

Продолжение [практической №2](https://github.com/manhattanattack/pr2). Перенесено из ветки `pr3`; всё находится в `main` этого отдельного репозитория.

**Unity:** 6000.4.6f1. **Сервер:** Node.js + WebSocket (`ws`).

## Как запустить

1. Склонируйте проект (лучше через GitHub Desktop с Git LFS) и откройте папку в Unity Hub версии 6000.4.6f1.
2. Дождитесь импорта файлов. **Никаких Tools → PR3, сборщиков и ручного создания интерфейса не требуется:** все изменения уже записаны в сцену `GameScene` и префабы.
3. Откройте `Assets/Scenes/LobbyScene.unity`. Убедитесь, что в Console нет красных ошибок.
4. В терминале в папке `Assets/MazeServer` выполните `npm install` (если нужно) и `node server.js`.
5. Нажмите Play и подключите второй клиент. В `GameScene` должны быть шкала HP, счетчик побед, меч, выстрелы и экран смерти.

Управление: **ЛКМ** — ближний удар (25 HP), **ПКМ** — снаряд (20 HP). После смерти — 3 секунды до возрождения со 100 HP.

Полный чек-лист — [README_PR3.md](README_PR3.md).

## Важно: Git LFS (спрайты и шрифты)

В GitHub-проект изначально были помещены указатели на LFS-ресурсы `.png` и `.ttf`; бинарные данные нужно отдельно перенести. Для этого в репозитории есть специальная команда:

1. Откройте вкладку **Actions** репозитория.
2. Выберите **Transfer original Git LFS files from PR2**.
3. Нажмите **Run workflow**, выберите `main` и дождитесь зелёной галочки.
4. Клонируйте `pr3` через GitHub Desktop или `git clone` с установленным Git LFS. ZIP-архив GitHub может не содержать настоящие бинарные LFS-файлы.

Если GitHub Actions недоступны, можно перенести LFS вручную:

```bash
git clone https://github.com/manhattanattack/pr2.git pr2-lfs
cd pr2-lfs
git lfs install
git lfs fetch --all origin
git remote add pr3 https://github.com/manhattanattack/pr3.git
git lfs push --all pr3
```

**Не считайте проект протестированным до запуска в Unity:** скрипты и данные подготовлены, но редактор Unity и две игровые сессии здесь не запускались.
