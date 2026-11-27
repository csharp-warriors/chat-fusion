
add_header X-Correlation-Id $correlation_id always;
add_header Strict-Transport-Security $hsts always;

location = /api/max/webhook {
    limit_req zone=webhooks burst=100 nodelay;
    proxy_pass http://max_adapter_svc;
}

location /api/users {
    limit_req zone=api burst=40 nodelay;
    proxy_pass http://identity_svc;
}

location /api/chats {
    limit_req zone=api burst=40 nodelay;
    proxy_pass http://hub_svc;
}

location /api/history/ {
    limit_req zone=api burst=40 nodelay;
    proxy_pass http://history_svc;
}

# SignalR: /hubs/chat и /hubs/chat/negotiate
location /hubs/ {
    proxy_pass http://webchat_svc;
    proxy_buffering off;
    proxy_read_timeout 1h;
    proxy_send_timeout 1h;
}

location /docs/identity/ { proxy_pass http://identity_svc/; }
location /docs/hub/      { proxy_pass http://hub_svc/; }
location /docs/history/  { proxy_pass http://history_svc/; }
location /docs/max/      { proxy_pass http://max_adapter_svc/; }

location = /identity/scalar {
    return 308 /identity/scalar/;
}

location ~ ^/identity/(scalar|openapi)(/.*)?$ {
    rewrite ^/identity/(.*)$ /$1 break;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header Accept-Encoding "";
    proxy_pass http://identity_svc;

    sub_filter_types text/html application/json;
    sub_filter_once off;
    sub_filter '/openapi/v1.json' '/identity/openapi/v1.json';
    sub_filter '"url":"openapi/v1.json"' '"url":"/identity/openapi/v1.json"';
    sub_filter '"http://chatfusion.csharp-warriors.ru/"' '"https://chatfusion.csharp-warriors.ru/identity/"';
    sub_filter '"https://chatfusion.csharp-warriors.ru/"' '"https://chatfusion.csharp-warriors.ru/identity/"';
}

location ^~ /identity/api/ {
    rewrite ^/identity/(api/.*)$ /$1 break;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_pass http://identity_svc;
}

location = /healthz {
    access_log off;
    default_type text/plain;
    return 200 "ok\n";
}

location / {
    proxy_pass http://frontend_svc;
}

# Telegram Adapter public routes.

location = /telegram-adapter/scalar {
    return 308 /telegram-adapter/scalar/;
}

location ~ ^/telegram-adapter/(scalar|openapi)(/.*)?$ {
    rewrite ^/telegram-adapter/(.*)$ /$1 break;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header Accept-Encoding "";
    proxy_pass http://telegram_adapter_svc;

    sub_filter_types text/html application/json;
    sub_filter_once off;
    sub_filter '/openapi/v1.json' '/telegram-adapter/openapi/v1.json';
    sub_filter '"url":"openapi/v1.json"' '"url":"/telegram-adapter/openapi/v1.json"';
    sub_filter '"http://chatfusion.csharp-warriors.ru/docs/telegram"' '"https://chatfusion.csharp-warriors.ru/telegram-adapter"';
    sub_filter '"https://chatfusion.csharp-warriors.ru/docs/telegram"' '"https://chatfusion.csharp-warriors.ru/telegram-adapter"';
    sub_filter '"http://chatfusion.csharp-warriors.ru/"' '"https://chatfusion.csharp-warriors.ru/telegram-adapter"';
    sub_filter '"https://chatfusion.csharp-warriors.ru/"' '"https://chatfusion.csharp-warriors.ru/telegram-adapter"';
    sub_filter '"http://chatfusion.csharp-warriors.ru"' '"https://chatfusion.csharp-warriors.ru/telegram-adapter"';
    sub_filter '"https://chatfusion.csharp-warriors.ru"' '"https://chatfusion.csharp-warriors.ru/telegram-adapter"';
}

location ^~ /telegram-adapter/api/ {
    rewrite ^/telegram-adapter/(api/.*)$ /$1 break;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_pass http://telegram_adapter_svc;
}
