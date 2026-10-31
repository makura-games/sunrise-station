# Playtime Stats

ui-playtime-stats-title = Игровое время пользователя
ui-playtime-overall-base = Общее игровое время:
ui-playtime-overall = Общее игровое время: { $time }
ui-playtime-first-time = Первый раз
ui-playtime-roles = Игровое время по должностям
ui-playtime-header-role-type = Должность
ui-playtime-header-role-time = Время
ui-playtime-time-format = { $hours }ч { $minutes }м
ui-playtime-time-format-verbose =
    { $hours ->
        [0] { $minutes } { $minutes ->
            [one] минута
            [few] минуты
           *[other] минут
        }
       *[other] { $hours } { $hours ->
            [one] час
            [few] часа
           *[other] часов
        } { $minutes } { $minutes ->
            [one] минута
            [few] минуты
           *[other] минут
        }
    }
ui-playtime-time-format-short =
    { $hours ->
        [0] { $minutes }м.
       *[other] { $hours }ч { $minutes }м.
    }
