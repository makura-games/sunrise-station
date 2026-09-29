server-ban-string-infinity = Вечно
server-ban-no-name = Не найдено. ({ $hwid })
server-time-ban =
    Временный бан на { $mins } { $mins ->
        [one] минуту
        [few] минуты
       *[other] минут
    }.
server-perma-ban = Перманентный бан.
server-role-ban =
    Временный джоб-бан на { $mins } { $mins ->
        [one] минуту
        [few] минуты
       *[other] минут
    }.
server-perma-role-ban = Перманентный джоб-бан.
server-time-ban-string =
    > **Администратор**
    > **Логин:** ``{ $adminName }``
    > **Дискорд:** { $adminLink }
    
    > **Нарушитель**
    > **Логин:** ``{ $targetName }``
    > **Дискорд:** { $targetLink }
    
    > **Выдан:** { $TimeNow }
    > **Истечёт:** { $expiresString }
    
    > **Причина:** { $reason }
    
    > **Уровень тяжести:** { $severity }
server-ban-footer = { $server } | Раунд: #{ $round }
server-perma-ban-string =
    > **Администратор**
    > **Логин:** ``{ $adminName }``
    > **Дискорд:** { $adminLink }
    
    > **Нарушитель**
    > **Логин:** ``{ $targetName }``
    > **Дискорд:** { $targetLink }
    
    > **Выдан:** { $TimeNow }
    
    > **Причина:** { $reason }
    
    > **Уровень тяжести:** { $severity }
server-role-ban-string =
    > **Администратор**
    > **Логин:** ``{ $adminName }``
    > **Дискорд:** { $adminLink }
    
    > **Нарушитель**
    > **Логин:** ``{ $targetName }``
    > **Дискорд:** { $targetLink }
    
    > **Выдан:** { $TimeNow }
    > **Истечёт:** { $expiresString }
    
    > **Роли:** { $roles }
    
    > **Причина:** { $reason }
    
    > **Уровень тяжести:** { $severity }
server-perma-role-ban-string =
    > **Администратор**
    > **Логин:** ``{ $adminName }``
    > **Дискорд:** { $adminLink }
    
    > **Нарушитель**
    > **Логин:** ``{ $targetName }``
    > **Дискорд:** ``{ $targetLink }``
    
    > **Выдан:** { $TimeNow }
    
    > **Роли:** { $roles }
    
    > **Причина:** { $reason }
    
    > **Уровень тяжести:** { $severity }
