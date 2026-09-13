analysis-console-menu-title = Широко-Спекторная Аналитическая Консоль Марк 3
analysis-console-server-list-button = Сервер
analysis-console-extract-button = Извлечь О.И.
analysis-console-info-no-scanner = Анализатор не подключён! Пожалуйста, подключите его с помощью мультитула.
analysis-console-info-no-artifact = Артефакт не найден! Поместите артефакт на платформу.
analysis-console-info-ready = Все системы запущены.
analysis-console-no-node = Выберите узел для просмотра
analysis-console-info-id = [font="Monospace" size=11]Айди:[/font]
analysis-console-info-id-value = [font="Monospace" size=11][color=yellow]{ $id }[/color][/font]
analysis-console-info-class = [font="Monospace" size=11]Классификация:[/font]
analysis-console-info-class-value = [font="Monospace" size=11]{ $class }[/font]
analysis-console-info-locked = [font="Monospace" size=11]Статус:[/font]
analysis-console-info-locked-value = [font="Monospace" size=11][color={ $state ->
        [0] red]Заблокировано
        [1] lime]Разблокировано
       *[2] plum]Активно
    }[/color][/font]
analysis-console-info-durability = [font="Monospace" size=11]Заряды:[/font]
analysis-console-info-durability-value = [font="Monospace" size=11][color={ $color }]{ $current }/{ $max }[/color][/font]
analysis-console-info-effect = [font="Monospace" size=11]Эффект:[/font]
analysis-console-info-effect-value = [font="Monospace" size=11][color=gray]{ $state ->
        [true] { $info }
       *[false] Разблокируйте узлы чтобы получить информацию
    }[/color][/font]
analysis-console-info-trigger = [font="Monospace" size=11]Тригеры:[/font]
analysis-console-info-triggered-value = [font="Monospace" size=11][color=gray]{ $triggers }[/color][/font]
analysis-console-info-scanner = Сканирование...
analysis-console-info-scanner-paused = Приостановлено.
analysis-console-progress-text =
    { $seconds ->
        [one] T-{ $seconds } секунда
       *[other] T-{ $seconds } секунд
    }
analysis-console-extract-value = [font="Monospace" size=11][color=orange]Узел { $id } (+{ $value })[/color][/font]
analysis-console-extract-none = [font="Monospace" size=11][color=orange] На разблокированных узлах не осталось очков для извлечения. [/color][/font]
analysis-console-extract-sum = [font="Monospace" size=11][color=orange]Всего О.И.: { $value }[/color][/font]
analyzer-artifact-extract-popup = Поверхность артефакта мерцает энергией!
