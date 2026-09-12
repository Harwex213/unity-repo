# Unity Monorepo

Монорепозиторий для Unity-проектов. Все бинарные ассеты хранятся в **Git LFS**.

Прочитайте раздел «Первый запуск» до первого клонирования. Если склонировать репозиторий без установленного Git LFS, вместо ассетов вы получите текстовые файлы-указатели, и Unity не откроет проект.

---

## Структура

```
.
├── projects/           # Unity-проекты, по одному на папку
│   ├── game-a/
│   └── game-b/
├── shared/             # Общие пакеты и ассеты между проектами
├── tools/              # Скрипты сборки и служебные утилиты
├── .gitattributes      # Правила Git LFS и правила слияния Unity YAML
└── .gitignore
```

---

## Требования

| Инструмент | Минимальная версия | Проверка |
| --- | --- | --- |
| Git | 2.30 | `git --version` |
| Git LFS | 3.0 | `git lfs version` |
| Unity Hub | любая | — |

### Установка Git LFS

**macOS**

```bash
brew install git-lfs
```

**Windows**

```powershell
winget install GitHub.GitLFS
```

**Linux (Debian/Ubuntu)**

```bash
sudo apt install git-lfs
```

---

## Первый запуск

### 1. Включите LFS для своего пользователя

Команда выполняется один раз на компьютере. Она прописывает LFS-фильтры в глобальный `~/.gitconfig`.

```bash
git lfs install
```

### 2. Склонируйте репозиторий

```bash
git clone <URL репозитория>
cd unity-repo
```

Git LFS подтянет бинарные файлы автоматически во время клонирования.

### 3. Проверьте, что ассеты скачались

```bash
git lfs ls-files | head
```

Команда выводит список файлов под LFS. Каждая строка начинается с хэша и символа `*` — звёздочка означает, что реальный файл лежит на диске. Символ `-` вместо звёздочки означает, что на диске остался указатель. В этом случае выполните:

```bash
git lfs pull
```

### 4. Откройте проект в Unity Hub

Добавьте в Unity Hub папку конкретного проекта — например `projects/game-a`, а не корень репозитория.

---

## Настройки Unity для каждого проекта

Проверьте эти настройки перед первым коммитом в новый проект. Без них Git не сможет сливать сцены и префабы.

**Edit → Project Settings → Editor**

- `Asset Serialization → Mode` = **Force Text**
- `Version Control → Mode` = **Visible Meta Files**

---

## Ежедневная работа

### Обычный цикл

```bash
git pull                  # LFS-файлы обновляются вместе с остальными
git add .
git commit -m "Добавил модель персонажа"
git push
```

Отдельных команд для LFS не требуется. Фильтр перехватывает файлы по расширению из `.gitattributes` и заливает их в LFS-хранилище при `git push`.

### Проверка перед коммитом

Убедитесь, что новый бинарник попал в LFS, а не в обычную историю Git:

```bash
git add MyModel.fbx
git lfs status
```

Файл должен оказаться в секции `Git LFS objects to be committed` с пометкой `LFS`. Пометка `Git` означает, что расширение не описано в `.gitattributes`.

---

## Добавление нового типа файлов в LFS

Допустим, в проекте появились файлы `.spine`.

1. Добавьте правило:

   ```bash
   git lfs track "*.spine"
   ```

   Команда дописывает строку в `.gitattributes`.

2. Закоммитьте сам `.gitattributes` **до** коммита новых файлов:

   ```bash
   git add .gitattributes
   git commit -m "LFS: отслеживаю *.spine"
   ```

3. Дальше добавляйте файлы обычным способом.

Порядок важен. Файл, закоммиченный до правила, уйдёт в обычную историю Git и останется там навсегда — до переписывания истории.

---

## Что делать, если бинарник попал в Git мимо LFS

Проблема выглядит так: репозиторий распухает, а `git lfs ls-files` не показывает файл.

Если ошибочный коммит ещё **не** запушен, перенесите файлы в LFS локально:

```bash
git lfs migrate import --include="*.fbx" --include-ref=HEAD~3..HEAD
```

Если коммит уже запушен, напишите в командный чат. Миграция переписывает историю, поэтому её делают согласованно: все участники после неё переклонируют репозиторий.

---

## Экономия трафика и места

Монорепозиторий хранит ассеты всех проектов. Полная выгрузка LFS может занимать десятки гигабайт. Скачайте только нужный проект.

### Клонирование без бинарников

```bash
GIT_LFS_SKIP_SMUDGE=1 git clone <URL репозитория>
cd unity-repo
git lfs pull --include="projects/game-a/**"
```

### Постоянное правило для репозитория

```bash
git config lfs.fetchinclude "projects/game-a/**,shared/**"
```

После этого `git pull` будет скачивать бинарники только указанных папок.

### Очистка локального кэша LFS

```bash
git lfs prune
```

Команда удаляет локальные копии старых версий файлов. Файлы текущей ветки остаются на месте.

---

## Слияние сцен и префабов

Unity хранит сцены и префабы в YAML. Обычный трёхсторонний merge Git ломает такие файлы. В `.gitattributes` для них указан драйвер `unityyamlmerge`. Пропишите его один раз в глобальном `~/.gitconfig`.

**macOS**

```bash
git config --global merge.unityyamlmerge.name "Unity YAML merge"
git config --global merge.unityyamlmerge.driver '/Applications/Unity/Hub/Editor/<ВЕРСИЯ>/Unity.app/Contents/Tools/UnityYAMLMerge merge -p %O %A %B %A'
```

**Windows**

```powershell
git config --global merge.unityyamlmerge.name "Unity YAML merge"
git config --global merge.unityyamlmerge.driver 'C:/Program Files/Unity/Hub/Editor/<ВЕРСИЯ>/Editor/Data/Tools/UnityYAMLMerge.exe merge -p %O %A %B %A'
```

Подставьте свою версию редактора вместо `<ВЕРСИЯ>`.

Драйвер не решает всех конфликтов. Большие сцены лучше делить на префабы и разграничивать зоны ответственности внутри команды.

---

## Блокировка файлов

Бинарный ассет нельзя слить. Два человека, правившие одну модель, получат конфликт, и одну из версий придётся выбросить. Для длинных правок берите блокировку.

```bash
git lfs lock projects/game-a/Assets/Art/Character.fbx   # взять
git lfs locks                                            # посмотреть занятое
git lfs unlock projects/game-a/Assets/Art/Character.fbx  # отпустить
```

Блокировки работают только на хостинге с поддержкой LFS File Locking — GitHub, GitLab, Azure DevOps.

---

## Частые проблемы

**Unity не видит текстуры, вместо картинок — пустые ассеты.**
На диске лежат указатели LFS. Выполните `git lfs pull`.

**`git lfs ls-files` ничего не выводит.**
LFS не включён для пользователя. Выполните `git lfs install`, затем `git checkout -- .`.

**`This repository is over its data quota`.**
Лимит LFS у хостинга исчерпан. Обратитесь к администратору репозитория.

**`Encountered N files that should have been pointers, but weren't`.**
Часть файлов закоммичена мимо LFS. Исправляется командой `git add --renormalize .` и коммитом результата.

**Конфликт в `.meta`-файле.**
`.meta` хранит GUID ассета. Берите версию той стороны, чей ассет остаётся в проекте. Никогда не удаляйте `.meta` вручную — Unity сгенерирует новый GUID и сломает все ссылки на ассет.

---

## Правила репозитория

1. Никогда не коммитьте папки `Library/`, `Temp/`, `Logs/`, `Build/`. Они уже в `.gitignore`.
2. Всегда коммитьте `.meta`-файлы вместе с ассетом.
3. Правило в `.gitattributes` добавляется отдельным коммитом, до появления самих файлов.
4. Большие ассеты храните в `shared/`, если их использует больше одного проекта.
