cmd-whitelistadd-desc = Добавить игрока в вайтлист сервера.
cmd-whitelistadd-help = Использование: whitelistadd <username>
cmd-whitelistadd-existing = { $username } уже находится в вайтлисте!
cmd-whitelistadd-added = { $username } добавлен в вайтлист
cmd-whitelistadd-not-found = Не удалось найти игрока '{ $username }'
cmd-whitelistadd-arg-player = [игрок]
cmd-whitelistremove-desc = Удалить игрока с вайтлиста сервера.
cmd-whitelistremove-help = Использование: whitelistremove <username>
cmd-whitelistremove-existing = { $username } не находится в вайтлисте!
cmd-whitelistremove-removed = { $username } удалён с вайтлиста
cmd-whitelistremove-not-found = Не удалось найти игрока '{ $username }'
cmd-whitelistremove-arg-player = [игрок]
cmd-kicknonwhitelisted-desc = Кикнуть всег игроков не в белом списке с сервера.
cmd-kicknonwhitelisted-help = Использование: kicknonwhitelisted
ban-banned-permanent = Этот бан можно только обжаловать.
ban-banned-permanent-appeal = Этот бан можно только обжаловать. Для этого посетите { $link }.
ban-expires = Вы получили бан на { $duration } минут, и он истечёт { $time } по UTC (для московского времени добавьте 3 часа).
ban-banned-1 = Вам, или другому пользователю этого компьютера или соединения, запрещено здесь играть.
ban-banned-2 = ID бана: { $id }
ban-banned-3 = Причина бана: "{ $reason }"
soft-player-cap-full = Сервер заполнен!
panic-bunker-account-denied = Этот сервер находится в режиме "Бункер", часто используемом в качестве меры предосторожности против рейдов. Новые подключения от аккаунтов, не соответствующих определённым требованиям, временно не принимаются. Повторите попытку позже
panic-bunker-account-denied-reason = Этот сервер находится в режиме "Бункер", часто используемом в качестве меры предосторожности против рейдов. Новые подключения от аккаунтов, не соответствующих определённым требованиям, временно не принимаются. Повторите попытку позже Причина: "{ $reason }"
panic-bunker-account-reason-account = Ваш аккаунт Space Station 14 слишком новый. Он должен быть старше { $minutes } минут
panic-bunker-account-reason-overall =
    Необходимо минимальное отыгранное Вами время на сервере — { $minutes } { $minutes ->
        [one] минута
        [few] минуты
       *[other] минут
    }.
whitelist-playtime = У вас недостаточно игрового времени для входа на этот сервер. Вам нужно как минимум { $minutes } минут игрового времени, чтобы присоединиться к этому серверу.
whitelist-player-count = В данный момент сервер не принимает новых игроков. Пожалуйста, попробуйте позже.
whitelist-notes = У вас слишком много администраторских заметок, чтобы присоединиться к этому серверу. Вы можете проверить свои заметки, введя в чат /adminremarks.
whitelist-manual = Вы не в списке разрешённых на этом сервере.
whitelist-blacklisted = Вы внесены в чёрный список на этом сервере.
whitelist-always-deny = Вам запрещено присоединяться к этому серверу.
whitelist-fail-prefix = Не в списке разрешённых: { $msg }
cmd-blacklistadd-desc = Добавляет игрока с указанным именем пользователя в чёрный список сервера.
cmd-blacklistadd-help = Использование: blacklistadd <username>
cmd-blacklistadd-existing = { $username } уже в чёрном списке!
cmd-blacklistadd-added = { $username } добавлен в чёрный список
cmd-blacklistadd-not-found = Не удалось найти '{ $username }'
cmd-blacklistadd-arg-player = [игрок]
cmd-blacklistremove-desc = Удаляет игрока с указанным именем пользователя из чёрного списка сервера.
cmd-blacklistremove-help = Использование: blacklistremove <логин>
cmd-blacklistremove-existing = { $username } не в чёрном списке!
cmd-blacklistremove-removed = { $username } удалён из чёрного списка
cmd-blacklistremove-not-found = Не удалось найти '{ $username }'
cmd-blacklistremove-arg-player = [игрок]
baby-jail-account-denied = Этот сервер предназначен для новичков и тех, кто хочет им помочь. Новые подключения от учетных записей, которые слишком старые или не находятся в белом списке, не принимаются. Попробуйте другие серверы и узнайте, что еще может предложить Space Station 14. Удачи!
baby-jail-account-denied-reason = Этот сервер предназначен для новичков и тех, кто хочет им помочь. Новые подключения от учетных записей, которые слишком старые или не находятся в белом списке, не принимаются. Попробуйте другие серверы и узнайте, что еще может предложить Space Station 14. Удачи! Причина: "{ $reason }"
baby-jail-account-reason-account = Ваша учетная запись Space Station 14 слишком старая. Она должна быть моложе { $minutes } минут.
baby-jail-account-reason-overall = Ваше общее время игры на сервере должно быть меньше { $minutes } минут.
generic-misconfigured = Сервер некорректно настроен и не принимает игроков. Пожалуйста, свяжитесь с владельцем сервера и повторите попытку позже.
ipintel-server-ratelimited = На этом сервере используется система безопасности с внешней проверкой, достигнут максимальный лимит проверок. Обратитесь к администрации сервера для помощи и повторите позже.
ipintel-unknown = На этом сервере используется система безопасности с внешней проверкой, но произошла ошибка. Обратитесь к администрации сервера и повторите попытку позже.
ipintel-suspicious = Обнаружено подключение через дата-центр или VPN. По правилам сервера VPN-соединения запрещены. Свяжитесь с администрацией, если это ошибка.
hwid-required = Ваш клиент отказался отправить идентификатор оборудования. Пожалуйста, свяжитесь с административной командой для получения дальнейшей помощи.
whitelist-not-whitelisted = Вас нет в вайтлисте.
# proper handling for having a min/max or not
whitelist-playercount-invalid =
    { $min ->
        [0] Вайтлист для этого сервера применяется только для числа игроков ниже { $max }.
       *[other]
            Вайтлист для этого сервера применяется только для числа игроков выше { $min } { $max ->
                [2147483647] ->  так что, возможно, вы сможете присоединиться позже.
               *[other] ->  и ниже { $max } игроков, так что, возможно, вы сможете присоединиться позже.
            }
    }
whitelist-not-whitelisted-rp = Вас нет в вайтлисте. Чтобы попасть в вайтлист, посетите наш Discord (ссылку можно найти по адресу https://discord.station14.ru).
ban-banned-4 = Попытки обойти этот бан, например, путём создания нового аккаунта, будут фиксироваться.
