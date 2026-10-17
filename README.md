# SPA-app: комментарии

SPA-приложение для комментариев с каскадными ответами. Тестовое задание dZENcode, уровень Junior+.

Демо: http://spa-comments-app.runasp.net/

## Возможности

- Комментарии с каскадными ответами любой глубины.
- Форма: User Name (латиница и цифры), E-mail, Home page (необязательно), CAPTCHA, Text, вложение.
- Главные комментарии в таблице: сортировка по User Name, E-mail и дате (в обе стороны), по умолчанию новые сверху (LIFO), по 25 на страницу. Ответы показываются внутри своей ветки и в пагинации не считаются.
- В тексте разрешены теги `<a href="" title="">`, `<code>`, `<i>`, `<strong>`. Теги должны быть закрыты и правильно вложены, остальное отклоняется.
- Панель кнопок `[i] [strong] [code] [a]` и предпросмотр без перезагрузки страницы.
- Вложения: JPG, GIF, PNG (больше 320×240 пропорционально уменьшается) и TXT до 100 КБ. Просмотр в lightbox.
- Валидация на клиенте и на сервере.
- Junior+: кэш (Redis), события, очередь, WebSocket (SignalR). Новые комментарии других пользователей приходят без обновления страницы, в виде плашки «Обновить».

## Стек

.NET 8, ASP.NET Core, Entity Framework Core, SQL Server 2022, Redis 7, SignalR, Angular, nginx, Docker.

## Быстрый запуск (Docker)

Нужен только Docker (Docker Desktop или Docker Engine с Compose v2).

```bash
git clone <адрес репозитория>
cd SPA-app
cp .env.example .env      # в Windows PowerShell: copy .env.example .env
```

Откройте `.env` и задайте свой `DB_PASSWORD`. Пароль SQL Server должен быть не короче 8 символов, с заглавными и строчными буквами, цифрой и спецсимволом, без символов `; $ " #`.

```bash
docker compose up -d --build
```

Первая сборка занимает несколько минут. Приложение откроется на `http://localhost:8080` (порт задаётся `WEB_PORT` в `.env`). Миграции база применяет сама при старте API.

Остановка: `docker compose down` (данные сохраняются в томах). Полная очистка вместе с данными: `docker compose down -v`.

## Разработка без Docker для приложения

Нужны .NET 8 SDK и Node.js 20+. База и Redis поднимаются в Docker:

```bash
docker compose up -d db redis
```

Строка подключения к базе хранится в user-secrets (в Git не попадает). Из папки `backend`:

```bash
dotnet user-secrets init --project src/CommentsApp.Api
dotnet user-secrets set "ConnectionStrings:Default" "Server=127.0.0.1,14330;Database=CommentsDb;User Id=sa;Password=<DB_PASSWORD из .env>;TrustServerCertificate=True" --project src/CommentsApp.Api
dotnet run --project src/CommentsApp.Api
```

Фронтенд (в другом терминале). Порт в `target` файла `frontend/proxy.conf.json` должен совпадать с портом API из консоли:

```bash
cd frontend
npm install
npm start
```

Приложение откроется на `http://localhost:4200`. В этом режиме загруженные файлы лежат в `backend/src/CommentsApp.Api/uploads/`, в Docker они хранятся в томе `uploads-data`.

## Тесты

```bash
cd backend
dotnet test
```

Покрыты санитайзер HTML, пагинация, сортировка и дерево ответов, кэш, публикация событий, очередь уведомлений. База в тестах - SQLite в памяти.

## Архитектура

```
backend/src/CommentsApp.Domain          сущности User, Comment, Attachment
backend/src/CommentsApp.Infrastructure  EF Core, DbContext, миграции
backend/src/CommentsApp.Api             контроллеры, сервисы, события, очередь, SignalR
frontend/                               Angular, nginx, Dockerfile
```

Запрос на создание комментария проходит по цепочке: проверка модели, санитайзер текста, проверка вложения в памяти, CAPTCHA, запись файла на диск, запись в БД. После сохранения публикуется событие `CommentCreated`, его обрабатывают три подписчика:

1. Сброс кэша списка (версия ключа в Redis меняется, старые страницы перестают читаться).
2. Постановка в очередь уведомления автору комментария, на который ответили. Очередь - `Channel<T>` с фоновым обработчиком (`BackgroundService`) и тремя попытками, вместо почты уведомление пишется в лог.
3. Рассылка уведомления о новом комментарии по SignalR.

Кэш недоступен: список читается из базы. CAPTCHA хранится в Redis, без него форма не отправится.

## API

| Метод | Адрес | Назначение |
|---|---|---|
| GET | `/api/comments?page=&sortBy=&desc=` | Страница главных комментариев с деревом ответов (`sortBy`: `date`, `username`, `email`) |
| POST | `/api/comments` | Создать комментарий или ответ (`multipart/form-data`) |
| POST | `/api/comments/preview` | Предпросмотр: вернуть очищенный HTML или ошибку |
| GET | `/api/captcha` | Новая CAPTCHA (`id` и картинка в base64) |
| GET | `/api/attachments/{id}` | Файл вложения |
| WS | `/hubs/comments` | События SignalR |

## Безопасность

- **XSS.** Сервер разбирает текст сам: тег вне белого списка отклоняется, текст между тегами экранируется, у `<a>` разрешены только `href` (только `http` и `https`) и `title`. Angular дополнительно очищает разметку при выводе.
- **SQL-инъекции.** Только запросы EF Core с параметрами. Поле сортировки выбирается из белого списка.
- **CAPTCHA** одноразовая, живёт 5 минут.
- **Файлы.** Тип определяется по содержимому, а не по расширению. Картинки перекодируются без метаданных (EXIF). Имя на диске генерируется (GUID), файлы отдаются с заголовком `nosniff`.
- **IP и User-Agent** автора сохраняются на сервере для идентификации клиента. За nginx используется `X-Forwarded-For`.

## База данных

Три таблицы: `Users`, `Comments` (самосвязь `ParentId` образует дерево ответов), `Attachments`. Один пользователь определяется парой «имя + e-mail».

Файлы схемы лежат в папке `docs/`:

- `schema.mwb` - схема для MySQL Workbench (EER-диаграмма);
- `schema.mysql.sql` - то же в виде SQL для MySQL;
- `schema.mssql.sql` - фактический скрипт для SQL Server, сгенерированный из миграций EF Core.

Рабочая база - SQL Server. Версия для MySQL сделана вручную по тем же сущностям, чтобы схему можно было открыть в Workbench и сравнить с задуманным.

## Известные ограничения

- Очередь в памяти: при перезапуске API невыполненные уведомления теряются.
- SignalR рассчитан на один экземпляр API. Для нескольких нужен Redis-backplane.
- Редактирования и удаления комментариев нет.
- Нагрузочные требования уровня Middle+ не реализованы.