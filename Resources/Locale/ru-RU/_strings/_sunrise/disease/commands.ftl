## Общее

cmd-disease-unknown-disease = Неизвестная болезнь { $disease }.
cmd-disease-not-infected-with = Сущность не больна { $disease }. Укажите код штамма из diseaselist.
cmd-disease-hint-disease = Код штамма или ID прототипа
cmd-disease-hint-stage = Номер стадии (с 1)

## diseaseinfect

cmd-diseaseinfect-desc = Заражает сущность болезнью в обход путей передачи, защиты и иммунитета.
cmd-diseaseinfect-help = Использование: { $command } <uid> <болезнь>
cmd-diseaseinfect-success = Сущность заражена болезнью { $disease }.
cmd-diseaseinfect-failed = Не удалось заразить: сущность уже больна { $disease } или болезнь некорректна.

## diseasecure

cmd-diseasecure-desc = Излечивает сущность от одной или всех болезней без выдачи иммунитета.
cmd-diseasecure-help = Использование: { $command } <uid> [болезнь]
cmd-diseasecure-success = Болезнь { $disease } излечена.
cmd-diseasecure-all-success = Все болезни излечены.
cmd-diseasecure-not-infected = Сущность не больна этой болезнью.

## diseasesetstage

cmd-diseasesetstage-desc = Переводит болезнь сущности на указанную стадию.
cmd-diseasesetstage-help = Использование: { $command } <uid> <болезнь> <стадия с 1>
cmd-diseasesetstage-success = Болезнь { $disease } переведена на стадию { $stage }.
cmd-diseasesetstage-failed = Не удалось сменить стадию: сущность не больна { $disease } или стадия вне диапазона 1–{ $count }.

## diseaselist

cmd-diseaselist-desc = Показывает болезни и иммунитеты сущности.
cmd-diseaselist-help = Использование: { $command } <uid>
cmd-diseaselist-none = Сущность ничем не больна.
cmd-diseaselist-entry = { $disease } [{ $strain }]: стадия { $stage }/{ $count } ({ $elapsed } с){ $recovering ->
        [true] , выздоравливает
       *[other] {""}
    }, подавление { $suppression } с, лечение { $cure }/{ $threshold }
cmd-diseaselist-symptoms = — симптомы (стадия появления): { $symptoms }
cmd-diseaselist-research = — сила { $strength }, исследование { $research }/{ $goal }, рецепт: { $solved }
cmd-diseaselist-symptoms-none = — симптомов нет
cmd-diseaselist-immunity-permanent = Иммунитет к { $disease }: постоянный
cmd-diseaselist-immunity-temporary = Иммунитет к { $disease }: ещё { $remaining } с

## diseaseoverlay

cmd-diseaseoverlay-desc = Переключает оверлей болезней: заражённые предметы, облака и носители с болезнями и стадиями.
cmd-diseaseoverlay-help = Использование: { $command }
cmd-diseaseoverlay-enabled = Оверлей болезней включён.
cmd-diseaseoverlay-disabled = Оверлей болезней выключен.
