# Blood Cult Console Commands

# Add Target Command
bloodcult-addtarget-description = Добавить цель для Культа Крови
bloodcult-addtarget-help = Добавляет конкретного игрока как цель Культа Крови для отслеживания и потенциального жертвоприношения.
bloodcult-addtarget-usage = Использование: bloodcult_addtarget <ckey>
bloodcult-addtarget-player-not-found = Игрок с ckey '{ $ckey }' не найден или не в игре.
bloodcult-addtarget-system-not-found = Система Культа Крови не найдена.
bloodcult-addtarget-rule-not-found = Активное правило Культа Крови не найдено.
bloodcult-addtarget-already-target = Сущность уже является целью культа.
bloodcult-addtarget-success = Цель культа { $name } успешно добавлена.
# Remove Target Command
bloodcult-removetarget-description = Удалить цель для Культа Крови
bloodcult-removetarget-help = Удаляет конкретного игрока из списка целей Культа Крови, прекращая отслеживание и отметку для жертвоприношения.
bloodcult-removetarget-usage = Использование: bloodcult_removetarget <ckey>
bloodcult-removetarget-player-not-found = Игрок с ckey '{ $ckey }' не найден или не в игре.
bloodcult-removetarget-system-not-found = Система Культа Крови не найдена.
bloodcult-removetarget-rule-not-found = Активное правило Культа Крови не найдено.
bloodcult-removetarget-not-target = Сущность не является целью культа.
bloodcult-removetarget-success = Цель культа { $name } успешно удалена.
# List Targets Command
bloodcult-listtargets-description = Показать все текущие цели Культа Крови
bloodcult-listtargets-help = Отображает все текущие цели Культа Крови с их статусом (жив или принесен в жертву) и информацией о сущности.
bloodcult-listtargets-usage = Использование: bloodcult_listtargets
bloodcult-listtargets-system-not-found = Система Культа Крови не найдена.
bloodcult-listtargets-no-targets = Цели культа не найдены.
bloodcult-listtargets-header =
    { $count ->
        [1] Текущая цель культа ({ $count }):
        [few] Текущие цели культа ({ $count }):
       *[other] Текущие цели культа ({ $count }):
    }
bloodcult-listtargets-sacrificed = Принесен в жертву
bloodcult-listtargets-alive = Жив
bloodcult-listtargets-target = { $name } ({ $uid }) - { $status }
bloodcult-unknown-entity = Неизвестная сущность
# Cult Device Alert
bloodcult-biocode-alert = Устройство пульсирует тёмной энергией, отвергая ваше прикосновение. Только те, кто связан кровью, могут владеть его силой.
