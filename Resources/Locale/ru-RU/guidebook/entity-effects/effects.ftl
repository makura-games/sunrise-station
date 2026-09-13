entity-effect-guidebook-spawn-entity =
    { $chance ->
        [1] Создаёт
        *[other] создают
    } { $amount ->
        [1] {INDEFINITE($entname)}
        *[other] {$amount} {MAKEPLURAL($entname)}
    }

entity-effect-guidebook-destroy =
    { $chance ->
        [1] Уничтожает
        *[other] уничтожают
    } объект

entity-effect-guidebook-break =
    { $chance ->
        [1] Ломает
        *[other] ломают
    } объект

entity-effect-guidebook-explosion =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } взрыв

entity-effect-guidebook-emp =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } электромагнитный импульс

entity-effect-guidebook-flash =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } ослепляющую вспышку

entity-effect-guidebook-foam-area =
    { $chance ->
        [1] Создаёт
        *[other] создают
    } большое количество пены

entity-effect-guidebook-smoke-area =
    { $chance ->
        [1] Создаёт
        *[other] создают
    } большое количество дыма

entity-effect-guidebook-satiate-thirst =
    { $chance ->
        [1] Утоляет
        *[other] утоляют
    } { $relative ->
        [1] жажду на средних значениях
        *[other] жажду со скоростью {NATURALFIXED($relative, 3)}x от средней
    }

entity-effect-guidebook-satiate-hunger =
    { $chance ->
        [1] Утоляет
        *[other] утоляют
    } { $relative ->
        [1] голод на средних значениях
        *[other] голод со скоростью {NATURALFIXED($relative, 3)}x от средней
    }

entity-effect-guidebook-health-change =
    { $chance ->
        [1] { $healsordeals ->
                [heals] Исцеляет
                [deals] Наносит
                *[both] Изменяет здоровье на
             }
        *[other] { $healsordeals ->
                    [heals] исцеляют
                    [deals] наносят
                    *[both] изменяют здоровье на
                 }
    } { $changes }

entity-effect-guidebook-even-health-change =
    { $chance ->
        [1] { $healsordeals ->
            [heals] Равномерно исцеляет
            [deals] Равномерно наносит
            *[both] Равномерно изменяет здоровье на
        }
        *[other] { $healsordeals ->
            [heals] равномерно исцеляют
            [deals] равномерно наносят
            *[both] равномерно изменяют здоровье на
        }
    } { $changes }

entity-effect-guidebook-status-effect =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                     *[other] вызывают
                 } {LOC($key)} минимум на {NATURALFIXED($time, 3)} {MANY("second", $time)} без накопления
        [add]   { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } {LOC($key)} минимум на {NATURALFIXED($time, 3)} {MANY("second", $time)} с накоплением
        [set]  { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } {LOC($key)} на {NATURALFIXED($time, 3)} {MANY("second", $time)} без накопления
        *[remove]{ $chance ->
                    [1] Снимает
                    *[other] снимают
                } {NATURALFIXED($time, 3)} {MANY("second", $time)} эффекта {LOC($key)}
    } { $delay ->
        [0] немедленно
        *[other] после задержки {NATURALFIXED($delay, 3)} {MANY("second", $delay)}
    }

entity-effect-guidebook-status-effect-indef =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                    *[other] вызывают
                 } постоянный эффект {LOC($key)}
        [add]   { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } постоянный эффект {LOC($key)}
        [set]  { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } постоянный эффект {LOC($key)}
        *[remove]{ $chance ->
                    [1] Снимает
                    *[other] снимают
                } эффект {LOC($key)}
    } { $delay ->
        [0] немедленно
        *[other] после задержки {NATURALFIXED($delay, 3)} {MANY("second", $delay)}
    }

entity-effect-guidebook-knockdown =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                    *[other] вызывают
                    } {LOC($key)} минимум на {NATURALFIXED($time, 3)} {MANY("second", $time)} без накопления
        [add]   { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } опрокидывание минимум на {NATURALFIXED($time, 3)} {MANY("second", $time)} с накоплением
        *[set]  { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } опрокидывание минимум на {NATURALFIXED($time, 3)} {MANY("second", $time)} без накопления
        [remove]{ $chance ->
                    [1] Снимает
                    *[other] снимают
                } {NATURALFIXED($time, 3)} {MANY("second", $time)} опрокидывания
    }

entity-effect-guidebook-set-solution-temperature-effect =
    { $chance ->
        [1] Устанавливает
        *[other] устанавливают
    } температуру раствора ровно {NATURALFIXED($temperature, 2)}k

entity-effect-guidebook-adjust-solution-temperature-effect =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Убирает
            }
        *[other]
            { $deltasign ->
                [1] добавляют
                *[-1] убирают
            }
    } тепло из раствора, пока температура { $deltasign ->
                [1] не превысит {NATURALFIXED($maxtemp, 2)}k
                *[-1] не опустится ниже {NATURALFIXED($mintemp, 2)}k
            }

entity-effect-guidebook-adjust-reagent-reagent =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Удаляет
            }
        *[other]
            { $deltasign ->
                [1] добавляют
                *[-1] удаляют
            }
    } {NATURALFIXED($amount, 2)}u реагента {$reagent} { $deltasign ->
        [1] в
        *[-1] из
    } раствора

entity-effect-guidebook-adjust-reagent-group =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Удаляет
            }
        *[other]
            { $deltasign ->
                [1] добавляют
                *[-1] удаляют
            }
    } {NATURALFIXED($amount, 2)}u реагентов группы {$group} { $deltasign ->
            [1] в
            *[-1] из
        } раствора

entity-effect-guidebook-adjust-temperature =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Убирает
            }
        *[other]
            { $deltasign ->
                [1] добавляют
                *[-1] убирают
            }
    } {POWERJOULES($amount)} тепла { $deltasign ->
            [1] к
            *[-1] из
        } тела, где находится

entity-effect-guidebook-chem-cause-disease =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } болезнь {$disease}

entity-effect-guidebook-chem-cause-random-disease =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } болезни {$diseases}

entity-effect-guidebook-jittering =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } дрожь

entity-effect-guidebook-clean-bloodstream =
    { $chance ->
        [1] Очищает
        *[other] очищают
    } кровоток от других веществ

entity-effect-guidebook-cure-disease =
    { $chance ->
        [1] Лечит
        *[other] лечат
    } болезни

entity-effect-guidebook-eye-damage =
    { $chance ->
        [1] { $deltasign ->
                [1] Наносит
                *[-1] Исцеляет
            }
        *[other]
            { $deltasign ->
                [1] наносят
                *[-1] исцеляют
            }
    } повреждение глаз

entity-effect-guidebook-vomit =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } рвоту

entity-effect-guidebook-create-gas =
    { $chance ->
        [1] Создаёт
        *[other] создают
    } { $moles } { $moles ->
        [1] моль
        *[other] молей
    } газа {$gas}

entity-effect-guidebook-drunk =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } опьянение

entity-effect-guidebook-electrocute =
    { $chance ->
        [1] Поражает током
        *[other] поражают током
    } метаболизирующего на {NATURALFIXED($time, 3)} {MANY("second", $time)}

entity-effect-guidebook-emote =
    { $chance ->
        [1] Заставит
        *[other] заставят
    } метаболизирующего выполнить [bold][color=white]{$emote}[/color][/bold]

entity-effect-guidebook-extinguish-reaction =
    { $chance ->
        [1] Тушит
        *[other] тушат
    } огонь

entity-effect-guidebook-flammable-reaction =
    { $chance ->
        [1] Повышает
        *[other] повышают
    } воспламеняемость

entity-effect-guidebook-ignite =
    { $chance ->
        [1] Поджигает
        *[other] поджигают
    } метаболизирующего

entity-effect-guidebook-make-sentient =
    { $chance ->
        [1] Делает
        *[other] делают
    } метаболизирующего разумным

entity-effect-guidebook-make-polymorph =
    { $chance ->
        [1] Превращает
        *[other] превращают
    } метаболизирующего в {$entityname}

entity-effect-guidebook-modify-bleed-amount =
    { $chance ->
        [1] { $deltasign ->
                [1] Усиливает
                *[-1] Ослабляет
            }
        *[other] { $deltasign ->
                    [1] усиливают
                    *[-1] ослабляют
                 }
    } кровотечение

entity-effect-guidebook-modify-blood-level =
    { $chance ->
        [1] { $deltasign ->
                [1] Повышает
                *[-1] Понижает
            }
        *[other] { $deltasign ->
                    [1] повышают
                    *[-1] понижают
                 }
    } уровень крови

entity-effect-guidebook-paralyze =
    { $chance ->
        [1] Парализует
        *[other] парализуют
    } метаболизирующего минимум на {NATURALFIXED($time, 3)} {MANY("second", $time)}

entity-effect-guidebook-movespeed-modifier =
    { $chance ->
        [1] Изменяет
        *[other] изменяют
    } скорость передвижения на {NATURALFIXED($sprintspeed, 3)}x минимум на {NATURALFIXED($time, 3)} {MANY("second", $time)}

entity-effect-guidebook-reset-narcolepsy =
    { $chance ->
        [1] Временно отгоняет
        *[other] временно отгоняют
    } нарколепсию

entity-effect-guidebook-wash-cream-pie-reaction =
    { $chance ->
        [1] Смывает
        *[other] смывают
    } кремовый пирог с лица

entity-effect-guidebook-cure-zombie-infection =
    { $chance ->
        [1] Лечит
        *[other] лечат
    } текущую зомби-инфекцию

entity-effect-guidebook-cause-zombie-infection =
    { $chance ->
        [1] Даёт
        *[other] дают
    } зомби-инфекцию

entity-effect-guidebook-innoculate-zombie-infection =
    { $chance ->
        [1] Лечит
        *[other] лечат
    } текущую зомби-инфекцию и даёт иммунитет от будущих

entity-effect-guidebook-reduce-rotting =
    { $chance ->
        [1] Восстанавливает
        *[other] восстанавливают
    } {NATURALFIXED($time, 3)} {MANY("second", $time)} гниения

entity-effect-guidebook-area-reaction =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } реакцию дыма или пены на {NATURALFIXED($duration, 3)} {MANY("second", $duration)}

entity-effect-guidebook-add-to-solution-reaction =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } добавление {$reagent} во внутренний контейнер раствора

entity-effect-guidebook-artifact-unlock =
    { $chance ->
        [1] Помогает
        *[other] помогают
        } разблокировать инопланетный артефакт.

entity-effect-guidebook-artifact-durability-restore =
    Восстанавливает {$restored} прочности в активных узлах артефакта.

entity-effect-guidebook-plant-attribute =
    { $chance ->
        [1] Регулирует
        *[other] регулируют
    } {$attribute} на {$positive ->
    [true] [color=red]{$amount}[/color]
    *[false] [color=green]{$amount}[/color]
    }

entity-effect-guidebook-plant-cryoxadone =
    { $chance ->
        [1] Омолаживает
        *[other] омолаживают
    } растение в зависимости от его возраста и времени роста

entity-effect-guidebook-plant-phalanximine =
    { $chance ->
        [1] Восстанавливает
        *[other] восстанавливают
    } жизнеспособность растения, утерянную из-за мутации

entity-effect-guidebook-plant-diethylamine =
    { $chance ->
        [1] Повышает
        *[other] повышают
    } срок жизни и/или базовое здоровье растения с шансом 10% для каждого

entity-effect-guidebook-plant-robust-harvest =
    { $chance ->
        [1] Повышает
        *[other] повышают
    } потенцию растения на {$increase} до максимума {$limit}. Растение теряет семена при достижении потенции {$seedlesstreshold}. Попытка поднять потенцию выше {$limit} может снизить урожайность с шансом 10%

entity-effect-guidebook-plant-seeds-add =
    { $chance ->
        [1] Возвращает
        *[other] возвращают
    } семена растения

entity-effect-guidebook-plant-seeds-remove =
    { $chance ->
        [1] Удаляет
        *[other] удаляют
    } семена растения

entity-effect-guidebook-plant-mutate-chemicals =
    { $chance ->
        [1] Мутирует
        *[other] мутируют
    } растение, чтобы производить {$name}

entity-effect-guidebook-status-effect-old =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                    *[other] вызвать
                } {LOC($key)} как минимум на {NATURALFIXED($time, 3)} {MANY("second", $time)} без накопления
        [add]   { $chance ->
                    [1] Вызывает
                    *[other] вызвать
                } {LOC($key)} как минимум на {NATURALFIXED($time, 3)} {MANY("second", $time)} с накоплением
        [set]   { $chance ->
                    [1] Вызывает
                    *[other] вызвать
                } {LOC($key)} на {NATURALFIXED($time, 3)} {MANY("second", $time)} без накопления
        *[remove]{ $chance ->
                    [1] Снимает
                    *[other] снять
                } {NATURALFIXED($time, 3)} {MANY("second", $time)} {LOC($key)}
    }
