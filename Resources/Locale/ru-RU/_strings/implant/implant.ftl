## Implanter Attempt Messages

implanter-component-implanting-target = { $user } пытается что-то в вас имплантировать!
implanter-component-implant-failed = { $implant } нельзя имплантировать в { $target }!
implanter-draw-failed-permanent = { $implant } вросся в { $target } и не может быть удалён!
implanter-draw-failed = Вы пытаетесь удалить имплант, но ничего не находите.
implanter-draw-failed-catastrophically = Имплантер ничего не находит и катастрофически отказывает, выбрасывая генетический материал в руку { $user }!
implanter-component-implant-already = { $target } уже имеет { $implant }!

## UI

implanter-set-draw-verb = Настроить извлечение импланта
implanter-set-draw-window = Настройка извлечения импланта
implanter-set-draw-info = Выберите тип импланта, который должен удалять этот имплантер:
implanter-set-draw-type = Тип импланта:
implanter-draw-text = Извлечение
implanter-inject-text = Установка
implanter-empty-text = Пусто
implanter-label-inject = [color=green]{ $implantName }[/color]
    Mode: [color=white]{ $modeString }[/color]
implanter-label-draw = [color=red]{ $implantName }[/color]
    Mode: [color=white]{ $modeString }[/color]
implanter-label = [color=green]{ $implantName }[/color]
    Режим: [color=white]{ $modeString }[/color]
implanter-contained-implant-text = [color=green]{ $desc }[/color]
action-name-toggle-fake-mindshield = [color=green]Контроль защиты разума[/color]
action-description-toggle-fake-mindshield = Активирует/деактивирует передачу сигнала имитатора защиты разума

mind-control-user-freed = Вы больше не находитесь под действием импланта. Вы не помните, что происходило под его воздействием, и кто вас имплантировал.
mind-control-user-briefing =
    Вам имплантировали имплант контроля разума. Ваш хозяин: [color=darkred]{$master-name}[/color].
    Выполняйте его приказы. Старайтесь не погибнуть, не получить травмы и не выдать имплант...
    Если только [color=darkred]{$master-name}[/color] не прикажет обратное.
mind-control-prevented = Имплантации помешал имплант защиты разума!
mind-control-prevents-mindshield = На пути оказался другой имплант.
mind-control-invalid = Цель должна быть живой.

## Implanter Actions

scramble-implant-activated-popup = Вы превратились в { $identity }
deathrattle-implant-dead-message = Зафиксирована смерть { $user } { $position }.
deathrattle-implant-critical-message = Жизненные показатели { $user } критические, требуется немедленная помощь { $position }.
