# «API Пульт» — ТЗ 1.1 (поправки к MVP 1.0)

**Статус:** принято к реализации  
**Дата:** 09.09.2026  
**База:** ТЗ MVP 1.0 от 09.09.2026  

Документ закрывает противоречия 1.0 и добавляет региональные сервисы. При конфликте с 1.0 действует эта версия.

## 1. Архитектурные решения

### ADR-1. CORS и прокси (F-REQ-1, NF-9)

**Решение:** прямые запросы из браузера — основной путь. Микросервисы **обязаны** отдавать CORS для origin Пульта.

Минимальный набор заголовков на целевом API:

- `Access-Control-Allow-Origin: <origin Пульта>` (не `*` при Authorization / cookie)
- `Access-Control-Allow-Methods: GET, POST, PUT, PATCH, DELETE, OPTIONS`
- `Access-Control-Allow-Headers: Authorization, Content-Type`
- Preflight `OPTIONS` → 204

В `services.yaml` у сервиса допускается флаг `proxy: true` — **зарезервирован**. В MVP-1 прокси не реализуется. Сервисы без CORS помечаются `proxy: true` и в UI отображаются как «только через прокси (следующая итерация)».

### ADR-2. JWT (F-AUTH-4 + NF-3)

**Решение:** основной транспорт — HttpOnly cookie.

| Параметр | Значение |
|---|---|
| Имя | `access_token` |
| HttpOnly | да |
| SameSite | Lax |
| Secure | да в non-Development |
| TTL | 8 часов |
| Path | `/` |

Бэкенд читает JWT из cookie. Fallback: заголовок `Authorization: Bearer` (Swagger, curl, автотесты). SPA токен в JS не хранит.

Локально фронт ходит на бэкенд через Vite proxy (`/api` → API), чтобы cookie была same-origin.

CSRF: same-origin через proxy + SameSite=Lax. Мутирующие запросы с фронта дополнительно шлют заголовок `X-Requested-With: XMLHttpRequest`. Cross-site cookie не используется.

F-AUTH-4 (1.1): «после логина бэкенд выставляет HttpOnly cookie; браузер передаёт её автоматически. Bearer допустим как fallback».

### ADR-3. mTLS (F-REQ-6)

**Решение (гибрид):**

- `auth.type: token` / `none` — запрос из браузера напрямую.
- `auth.type: certificate` **без** `proxy` — Пульт бьёт в URL сервиса; клиентский сертификат выбирает **браузер** (сертификат должен быть в хранилище ОС). Поле `cert_path` **игнорируется**.
- `auth.type: certificate` **с** `proxy: true` — в следующей итерации бэкенд использует `cert_path` / секрет из Vault. В MVP-1 такие сервисы не отправляются из UI.

F-REQ-6 помечается как spike до проверки на реальном mTLS-сервисе.

### ADR-4. RBAC при прямых запросах (F-RBAC-5)

Пульт **не** является PEP для микросервисов. F-RBAC-5 (1.1): «Пульт не отдаёт контракт, форму Send, шаблоны и генерацию DTO для сервисов вне прав пользователя». Реальную авторизацию выполняют сами сервисы.

### ADR-5. Prod

Перед Send в `prod` UI показывает подтверждение (сервис, регион, URL). Отдельной роли на prod в MVP нет.

### ADR-6. Региональные сервисы (новое)

Часть сервисов имеет **разные base URL на одну и ту же среду в разных регионах**. Контракт (Swagger) обычно общий.

UX:

- У обычного сервиса — только переключатель среды (dev / stage / prod).
- У регионального — среда **и** регион (сегмент RU | KZ | …). Селектор региона показывается только если у сервиса есть регионы.
- Вкладка: `Catalog · RU · stage`.
- Последний выбранный регион запоминается per-service (sessionStorage).
- `default_region` из YAML — начальное значение.
- История хранит `region_code`.

Два способа описать URL (можно смешивать в одном файле):

1. **Шаблон** `{region}` в URL среды — когда паттерн одинаковый.
2. **Явные URL на регион** — когда хосты несимметричны.

## 2. Функциональные правки

| ID | Изменение |
|---|---|
| F-AUTH-4 | Cookie HttpOnly; Bearer — fallback |
| F-AUTH-9 | При `is_first_login = true` пользователь обязан сменить пароль до работы с пультом (средний) |
| F-RBAC-5 | Формулировка ADR-4 |
| F-CFG-2 | Добавлены `proxy`, `regions`, `default_region`, шаблон `{region}` |
| F-CFG-6 | Региональные URL: селектор региона, persist, запись в историю |
| F-REQ-6 | Spike; `cert_path` только вместе с будущим proxy |
| F-SWAG-4 | Snapshot raw JSON в `contract_snapshots`; `endpoints` — только актуальная проекция |
| F-HIST-9 | Тело ответа режется до 256 KB, флаг `response_truncated` |
| F-HIST-10 | MVP хранит только JSON-тела запросов |
| F-ADMIN-1 | SMTP нет: админ задаёт пароль в UI |
| NF-9 | CORS на целевых микросервисах обязателен для прямого Send |
| NF-10 | Hot-reload YAML: каждый под перечитывает файл по кнопке/таймеру 60 с (без Redis) |

## 3. YAML (1.1)

```yaml
services:
  - name: order-service
    description: Управление заказами
    color: "#4CAF50"
    proxy: false
    swagger:
      url: https://api.dev.company.com/order/swagger/v1/swagger.json
      auth: basic
      vault_path: secret/swagger/order-service
    environments:
      dev: https://api.dev.company.com/order
      stage: https://api.stage.company.com/order
      prod: https://api.prod.company.com/order
    auth:
      type: token

  # Шаблон региона в URL
  - name: notify-service
    description: Региональные уведомления
    color: "#9C27B0"
    default_region: ru
    swagger:
      url: https://notify.dev.ru.company.com/swagger/v1/swagger.json
      auth: none
    regions:
      - code: ru
        label: Россия
      - code: kz
        label: Казахстан
    environments:
      dev: https://notify.dev.{region}.company.com
      stage: https://notify.stage.{region}.company.com
      prod: https://notify.prod.{region}.company.com
    auth:
      type: token

  # Явные URL на регион (хосты несимметричны)
  - name: geo-catalog-service
    description: Региональный каталог
    color: "#00BCD4"
    default_region: ru
    swagger:
      url: https://catalog.dev.company.com/swagger/v1/swagger.json
      auth: none
    regions:
      - code: ru
        label: Россия
        environments:
          dev: https://catalog-ru.dev.company.com
          stage: https://catalog-ru.stage.company.com
          prod: https://catalog.company.ru
      - code: kz
        label: Казахстан
        environments:
          dev: https://catalog-kz.dev.company.com
          stage: https://catalog-kz.stage.company.com
          prod: https://catalog.company.kz
    auth:
      type: token

  - name: payment-service
    description: Платежи (mTLS)
    color: "#FF9800"
    proxy: false
    swagger:
      url: https://api.dev.company.com/payment/swagger/v1/swagger.json
      auth: none
    environments:
      dev: https://api.dev.company.com/payment
      stage: https://api.stage.company.com/payment
      prod: https://api.prod.company.com/payment
    auth:
      type: certificate
      cert_path: /certs/payment-service.pfx
```

Правила резолва base URL:

1. Нет `regions` → `environments[env]`, `region_code = ""`.
2. Есть `regions` и у региона свой `environments[env]` → он.
3. Иначе подставить `{region}` в `environments[env]` сервиса.
4. Иначе ошибка загрузки конфига по этому сервису (остальные загружаются).

Swagger в MVP качается **один раз на сервис** (общий контракт). Per-region swagger — следующая итерация.

## 4. Модель данных (исправленная)

```sql
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    display_name VARCHAR(255),
    password_hash VARCHAR(255) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_first_login BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE roles (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) UNIQUE NOT NULL,
    description TEXT
);

CREATE TABLE services (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) UNIQUE NOT NULL,
    description TEXT,
    color VARCHAR(7),
    swagger_url VARCHAR(500),
    swagger_auth_type VARCHAR(20) NOT NULL DEFAULT 'none'
        CHECK (swagger_auth_type IN ('basic', 'none')),
    swagger_vault_path VARCHAR(500),
    auth_type VARCHAR(20) NOT NULL DEFAULT 'none'
        CHECK (auth_type IN ('token', 'certificate', 'none')),
    cert_path VARCHAR(500),
    proxy BOOLEAN NOT NULL DEFAULT FALSE,
    is_regional BOOLEAN NOT NULL DEFAULT FALSE,
    default_region VARCHAR(50),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE service_regions (
    id SERIAL PRIMARY KEY,
    service_id INT NOT NULL REFERENCES services(id) ON DELETE CASCADE,
    code VARCHAR(50) NOT NULL,
    label VARCHAR(100) NOT NULL,
    sort_order INT NOT NULL DEFAULT 0,
    UNIQUE (service_id, code)
);

-- region_code = '' для нерегиональных сервисов (уникальность без NULL)
CREATE TABLE service_urls (
    id SERIAL PRIMARY KEY,
    service_id INT NOT NULL REFERENCES services(id) ON DELETE CASCADE,
    environment VARCHAR(20) NOT NULL CHECK (environment IN ('dev', 'stage', 'prod')),
    region_code VARCHAR(50) NOT NULL DEFAULT '',
    base_url VARCHAR(500) NOT NULL,
    UNIQUE (service_id, environment, region_code)
);

CREATE TABLE user_role_assignments (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id INT NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    service_id INT REFERENCES services(id) ON DELETE CASCADE,
    granted_by INT REFERENCES users(id),
    granted_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX uq_user_role_service
    ON user_role_assignments (user_id, role_id, service_id)
    WHERE service_id IS NOT NULL;

CREATE UNIQUE INDEX uq_user_role_global
    ON user_role_assignments (user_id, role_id)
    WHERE service_id IS NULL;

CREATE TABLE role_audit_log (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id),
    changed_by INT NOT NULL REFERENCES users(id),
    action VARCHAR(20) NOT NULL CHECK (action IN ('grant', 'revoke')),
    role_id INT REFERENCES roles(id),
    service_id INT REFERENCES services(id),
    changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE endpoints (
    id SERIAL PRIMARY KEY,
    service_id INT NOT NULL REFERENCES services(id) ON DELETE CASCADE,
    path VARCHAR(500) NOT NULL,
    method VARCHAR(10) NOT NULL,
    description TEXT,
    operation_id VARCHAR(100),
    request_schema JSONB,
    response_schema JSONB,
    tags TEXT[],
    UNIQUE (service_id, path, method)
);

CREATE TABLE contract_snapshots (
    id SERIAL PRIMARY KEY,
    service_id INT NOT NULL REFERENCES services(id) ON DELETE CASCADE,
    fetched_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    raw_json JSONB NOT NULL
);

CREATE INDEX idx_contract_snapshots_service_fetched
    ON contract_snapshots (service_id, fetched_at DESC);

CREATE TABLE request_history (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    service_id INT REFERENCES services(id),
    endpoint_id INT REFERENCES endpoints(id),
    environment VARCHAR(20),
    region_code VARCHAR(50) NOT NULL DEFAULT '',
    url VARCHAR(500),
    method VARCHAR(10),
    request_headers JSONB,
    request_body JSONB,
    response_status INT,
    response_body TEXT,
    response_truncated BOOLEAN NOT NULL DEFAULT FALSE,
    response_time_ms INT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_request_history_user_created
    ON request_history (user_id, created_at DESC);

CREATE TABLE request_templates (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    endpoint_id INT NOT NULL REFERENCES endpoints(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    template_body JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE OR REPLACE FUNCTION trim_request_history()
RETURNS trigger AS $$
BEGIN
    DELETE FROM request_history
    WHERE id IN (
        SELECT id FROM request_history
        WHERE user_id = NEW.user_id
        ORDER BY created_at DESC, id DESC
        OFFSET 100
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_trim_request_history
AFTER INSERT ON request_history
FOR EACH ROW
EXECUTE FUNCTION trim_request_history();

CREATE OR REPLACE FUNCTION get_roles_hash()
RETURNS TEXT AS $$
DECLARE
    hash TEXT;
BEGIN
    SELECT MD5(string_agg(
        CONCAT(user_id, ':', role_id, ':', COALESCE(service_id, 0)),
        ';' ORDER BY user_id, role_id, service_id
    ))
    INTO hash
    FROM user_role_assignments;
    RETURN COALESCE(hash, 'empty');
END;
$$ LANGUAGE plpgsql;
```

Лимит тела ответа 256 KB применяется в приложении до INSERT (`History:MaxResponseBodyBytes`).
