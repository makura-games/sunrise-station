markings-search = Поиск
-markings-selection =
    { $selectable ->
        [0] Больше нельзя выбрать ни одной черты.
        [one] Можно выбрать ещё одну черту.
        [few] Можно выбрать ещё { $selectable } черты.
       *[other] Можно выбрать ещё { $selectable } черт.
    }
markings-limits =
    { $required ->
        [true]
            { $count ->
                [-1] Выберите хотя бы одну черту.
                [0] Нельзя выбрать ни одной черты, но одна почему-то обязательна. Это ошибка.
                [one] Выберите одну черту.
               *[other] Выберите от одной до { $count } черт. { -markings-selection(selectable: $selectable) }
            }
       *[false]
            { $count ->
                [-1] Можно выбрать любое количество черт.
                [0] Нельзя выбрать ни одной черты.
                [one] Можно выбрать не более одной черты.
               *[other] Можно выбрать не более { $count } черт. { -markings-selection(selectable: $selectable) }
            }
    }
markings-reorder = Изменить порядок черт
humanoid-marking-modifier-respect-limits = Учитывать ограничения
humanoid-marking-modifier-respect-group-sex = Учитывать ограничения группы и пола
humanoid-marking-modifier-base-layers = Базовые слои
humanoid-marking-modifier-enable = Включить
humanoid-marking-modifier-prototype-id = ID прототипа:

# Categories

markings-organ-Torso = Торс
markings-organ-Head = Голова
markings-organ-ArmLeft = Левая рука
markings-organ-ArmRight = Правая рука
markings-organ-HandRight = Правая кисть
markings-organ-HandLeft = Левая кисть
markings-organ-LegLeft = Левая нога
markings-organ-LegRight = Правая нога
markings-organ-FootLeft = Левая стопа
markings-organ-FootRight = Правая стопа
markings-organ-Eyes = Глаза
markings-layer-Special = Специальное
markings-layer-Tail = Хвост
markings-layer-Tail-Moth = Крылья
markings-layer-Hair = Причёска
markings-layer-FacialHair = Лицевая растительность
markings-layer-UndergarmentTop = Верхнее бельё
markings-layer-UndergarmentBottom = Нижнее бельё
markings-layer-Chest = Грудь
markings-layer-Head = Голова
markings-layer-Snout = Морда
markings-layer-SnoutCover = Морда (покров)
markings-layer-HeadSide = Голова (бок)
markings-layer-HeadTop = Голова (верх)
markings-layer-Eyes = Глаза
markings-layer-RArm = Правая рука
markings-layer-LArm = Левая рука
markings-layer-RHand = Правая кисть
markings-layer-LHand = Левая кисть
markings-layer-RLeg = Правая нога
markings-layer-LLeg = Левая нога
markings-layer-RFoot = Правая стопа
markings-layer-LFoot = Левая стопа
markings-layer-Overlay = Наложение
markings-layer-TailOverlay = Наложение на хвост
markings-used = Используемые черты
markings-unused = Неиспользуемые черты
markings-add = Добавить черту
markings-remove = Убрать черту
markings-rank-up = Вверх
markings-rank-down = Вниз
marking-points-remaining = Черт осталось: { $points }
marking-used = { $marking-name }
marking-used-forced = { $marking-name } (Принудительно)
marking-slot-add = Добавить
marking-slot-remove = Удалить
marking-slot = Слот { $number }
markings-category-Special = Специальное
markings-category-Hair = Причёска
markings-category-FacialHair = Лицевая растительность
markings-category-Head = Голова
markings-category-HeadTop = Голова (верх)
markings-category-HeadSide = Голова (бок)
markings-category-Snout = Морда
markings-category-UndergarmentTop = Верхнее бельё
markings-category-UndergarmentBottom = Нижнее бельё
markings-category-Chest = Грудь
markings-category-Arms = Руки
markings-category-Legs = Ноги
markings-category-Tail = Хвост
markings-category-Overlay = Наложение
markings-category-Back = Спина
