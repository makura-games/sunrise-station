humanoid-marking-modifier-force = Принудительно
humanoid-marking-modifier-ignore-species = Игнорировать вид
humanoid-marking-modifier-base-layers = Базовые слои
humanoid-marking-modifier-enable = Включить
humanoid-marking-modifier-prototype-id = ID прототипа:

markings-category-SnoutCover = Морда (покрытие)
markings-category-Dreadlocks = Дреды
markings-category-Rings = Кольца

-markings-selection =
    { $selectable ->
        [0] Больше нельзя выбрать ни одной черты.
        [one] Можно выбрать ещё одну черту.
        [few] Можно выбрать ещё { $selectable } черты.
       *[other] Можно выбрать ещё { $selectable } черт.
    }
markings-limits =
    { $required ->
        [true] { $count ->
            [-1] Выберите хотя бы одну черту.
            [0] Нельзя выбрать ни одной черты, но одна почему-то обязательна. Это ошибка.
            [one] Выберите одну черту.
           *[other] Выберите от одной до { $count } черт. { -markings-selection(selectable: $selectable) }
        }
       *[false] { $count ->
            [-1] Можно выбрать любое количество черт.
            [0] Нельзя выбрать ни одной черты.
            [one] Можно выбрать не более одной черты.
           *[other] Можно выбрать не более { $count } черт. { -markings-selection(selectable: $selectable) }
        }
    }
markings-reorder = Изменить порядок черт

humanoid-marking-modifier-respect-limits = Учитывать ограничения
humanoid-marking-modifier-respect-group-sex = Учитывать ограничения группы и пола

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

markings-layer-TailOverlay = Наложение на хвост
markings-layer-Back = Спина

