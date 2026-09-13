device-pda-slot-component-slot-name-cartridge = Картридж
default-program-name = Программа
notekeeper-program-name = Заметки
nano-task-program-name = НаноТаск
news-read-program-name = Новости станции
crew-manifest-program-name = Манифест экипажа
messenger-program-name = РобастЧат
crew-manifest-cartridge-loading = Загрузка...
net-probe-program-name = Зонд сетей
net-probe-scan = Просканирован { $device }!
net-probe-label-name = Название
net-probe-label-address = Адрес
net-probe-label-frequency = Частота
net-probe-label-network = Сеть
log-probe-program-name = Зонд логов
log-probe-scan = Загружены логи устройства { $device }!
log-probe-label-time = Время
log-probe-label-accessor = Использовано:
log-probe-label-number = #
log-probe-print-button = Распечатать логи
log-probe-printout-device = Просканированное устройство: { $name }
log-probe-printout-header = Последние логи:
log-probe-printout-entry = #{ $number } / { $time } / { $accessor }
astro-nav-program-name = АстроНав
navigator-program-name = Навигатор
navigator-cartridge-loading = Загрузка карты...
photo-program-name = ФлешСнэп

med-tek-program-name = МедТек
# Wanted list cartridge
wanted-list-program-name = Список разыскиваемых
nano-task-ui-heading-high-priority-tasks =
    { $amount ->
        [zero] Нет задач высокого приоритета
        [one] 1 задача высокого приоритета
       *[other] { $amount } задач высокого приоритета
    }
nano-task-ui-heading-medium-priority-tasks =
    { $amount ->
        [zero] Нет задач среднего приоритета
        [one] 1 задача среднего приоритета
       *[other] { $amount } задач среднего приоритета
    }
nano-task-ui-heading-low-priority-tasks =
    { $amount ->
        [zero] Нет задач низкого приоритета
        [one] 1 задача низкого приоритета
       *[other] { $amount } задач низкого приоритета
    }
nano-task-ui-done = Выполнено
nano-task-ui-revert-done = Отменить
nano-task-ui-priority-low = Низкий
nano-task-ui-priority-medium = Средний
nano-task-ui-priority-high = Высокий
nano-task-ui-cancel = Отмена
nano-task-ui-print = Печать
nano-task-ui-delete = Удалить
nano-task-ui-save = Сохранить
nano-task-ui-new-task = Новая задача
nano-task-ui-description-label = Описание:
nano-task-ui-description-placeholder = Взять что-то важное
nano-task-ui-requester-label = Запрашивающий:
nano-task-ui-requester-placeholder = Иван Нанотрейзен
nano-task-ui-item-title = Редактировать задачу
nano-task-printed-description = Описание: { $description }
nano-task-printed-requester = Запрашивающий: { $requester }
nano-task-printed-high-priority = Приоритет: Высокий
nano-task-printed-medium-priority = Приоритет: Средний
nano-task-printed-low-priority = Приоритет: Низкий
wanted-list-label-no-records = Все в порядке, ковбой
wanted-list-search-placeholder = Поиск по имени и статусу
wanted-list-age-label = [color=darkgray]Возвраст:[/color] [color=white]{ $age }[/color]
wanted-list-job-label = [color=darkgray]Работа:[/color] [color=white]{ $job }[/color]
wanted-list-species-label = [color=darkgray]Раса:[/color] [color=white]{ $species }[/color]
wanted-list-gender-label = [color=darkgray]Гендр:[/color] [color=white]{ $gender }[/color]
wanted-list-reason-label = [color=darkgray]Причина:[/color] [color=white]{ $reason }[/color]
wanted-list-unknown-reason-label = неизвестная причина
wanted-list-initiator-label = [color=darkgray]Инициатор:[/color] [color=white]{ $initiator }[/color]
wanted-list-unknown-initiator-label = неизвестный инициатор
wanted-list-status-label = [color=darkgray]статус:[/color] { $status ->
        [suspected] [color=yellow]подозреваемый[/color]
        [wanted] [color=red]разыскиваемый[/color]
        [detained] [color=#b18644]задержан[/color]
        [paroled] [color=green]на условно-досрочном[/color]
        [discharged] [color=green]освобожден[/color]
       *[other] нет данных
    }
wanted-list-history-table-time-col = Время
wanted-list-history-table-reason-col = Нарушение
wanted-list-history-table-initiator-col = Инициатор
