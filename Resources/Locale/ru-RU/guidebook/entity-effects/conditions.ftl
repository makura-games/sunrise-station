entity-condition-guidebook-total-damage =
    { $max ->
        [2147483648] имеет по крайней мере {NATURALFIXED($min, 2)} суммарного урона
        *[other] { $min ->
                    [0] имеет не более {NATURALFIXED($max, 2)} суммарного урона
                    *[other] имеет от {NATURALFIXED($min, 2)} до {NATURALFIXED($max, 2)} суммарного урона
                 }
    }

entity-condition-guidebook-type-damage =
    { $max ->
        [2147483648] имеет по крайней мере {NATURALFIXED($min, 2)} урона {$type}
        *[other] { $min ->
                    [0] имеет не более {NATURALFIXED($max, 2)} урона {$type}
                    *[other] имеет от {NATURALFIXED($min, 2)} до {NATURALFIXED($max, 2)} урона {$type}
                 }
    }

entity-condition-guidebook-group-damage =
    { $max ->
        [2147483648] имеет по крайней мере {NATURALFIXED($min, 2)} урона {$type}.
        *[other] { $min ->
                    [0] имеет не более {NATURALFIXED($max, 2)} урона {$type}.
                    *[other] имеет от {NATURALFIXED($min, 2)} до {NATURALFIXED($max, 2)} урона {$type}
                 }
    }

entity-condition-guidebook-total-hunger =
    { $max ->
        [2147483648] цель имеет по крайней мере {NATURALFIXED($min, 2)} общего голода
        *[other] { $min ->
                    [0] цель имеет не более {NATURALFIXED($max, 2)} общего голода
                    *[other] цель имеет от {NATURALFIXED($min, 2)} до {NATURALFIXED($max, 2)} общего голода
                 }
    }

entity-condition-guidebook-reagent-threshold =
    { $max ->
        [2147483648] имеется по крайней мере {NATURALFIXED($min, 2)}u вещества {$reagent}
        *[other] { $min ->
                    [0] имеется не более {NATURALFIXED($max, 2)}u вещества {$reagent}
                    *[other] имеется от {NATURALFIXED($min, 2)}u до {NATURALFIXED($max, 2)}u вещества {$reagent}
                 }
    }

entity-condition-guidebook-mob-state-condition =
    моб находится в состоянии { $state }

entity-condition-guidebook-job-condition =
    должность цели — { $job }

entity-condition-guidebook-solution-temperature =
    температура раствора { $max ->
            [2147483648] составляет по крайней мере {NATURALFIXED($min, 2)}k
            *[other] { $min ->
                        [0] составляет не более {NATURALFIXED($max, 2)}k
                        *[other] составляет от {NATURALFIXED($min, 2)}k до {NATURALFIXED($max, 2)}k
                     }
    }

entity-condition-guidebook-body-temperature =
    температура тела { $max ->
            [2147483648] составляет по крайней мере {NATURALFIXED($min, 2)}k
            *[other] { $min ->
                        [0] составляет не более {NATURALFIXED($max, 2)}k
                        *[other] составляет от {NATURALFIXED($min, 2)}k до {NATURALFIXED($max, 2)}k
                     }
    }

entity-condition-guidebook-organ-type =
    метаболизирующий орган { $shouldhave ->
                                [true] является
                                *[false] не является
                           } {INDEFINITE($name)} органом {$name}

entity-condition-guidebook-has-tag =
    цель { $invert ->
                 [true] не имеет
                 *[false] имеет
                } тег {$tag}

entity-condition-guidebook-this-reagent = этот реагент

entity-condition-guidebook-breathing =
    метаболизатор { $isBreathing ->
                [true] дышит нормально
                *[false] задыхается
               }

entity-condition-guidebook-internals =
    метаболизатор { $usingInternals ->
                [true] использует внутренние системы
                *[false] дышит атмосферным воздухом
               }
