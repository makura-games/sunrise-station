ingestion-you-need-to-hold-utensil = Вам нужно держать {INDEFINITE($utensil)} {$utensil}, чтобы это съесть!

ingestion-try-use-is-empty = {CAPITALIZE($entity)} пуст!
ingestion-try-use-wrong-utensil = Вы не можете {$verb} {$food} с {INDEFINITE($utensil)} {$utensil}.

ingestion-remove-mask = Сначала вам нужно снять {$entity}.

## Failed Ingestion

ingestion-you-cannot-ingest-any-more = Вы не можете {$verb} больше!
ingestion-other-cannot-ingest-any-more = {CAPITALIZE(SUBJECT($target))} не может {$verb} больше!

ingestion-cant-digest = Вы не можете переварить {$entity}!
ingestion-cant-digest-other = {CAPITALIZE(SUBJECT($target))} не может переварить {$entity}!

## Action Verbs, not to be confused with Verbs

ingestion-verb-food = Есть
ingestion-verb-drink = Пить

# Edible Component

-edible-satiated = { $satiated ->
    [true] {" "}Вам больше не хочется { $verb }.
  *[false] {""}
}

edible-nom = Ням. {$flavors}{ -edible-satiated(satiated: $satiated, verb: "eat") }
edible-nom-other = Ням.
edible-slurp = Слёрп. {$flavors}{ -edible-satiated(satiated: $satiated, verb: "drink") }
edible-slurp-other = Слёрп.
edible-swallow = Вы глотаете { $food }.{ -edible-satiated(satiated: $satiated, verb: "swallow") }
edible-gulp = Глоток. {$flavors}
edible-gulp-other = Глоток.

edible-has-used-storage = Вы не можете {$verb} { $food } с предметом внутри.

## Nouns

edible-noun-edible = съедобный
edible-noun-food = еда
edible-noun-drink = напиток
edible-noun-pill = таблетка

## Verbs

edible-verb-edible = поглощать
edible-verb-food = есть
edible-verb-drink = пить
edible-verb-pill = глотать

## Force feeding

edible-force-feed = {CAPITALIZE($user)} пытается заставить вас {$verb} что-то!
edible-force-feed-success = {CAPITALIZE($user)} заставил вас {$verb} что-то! {$flavors}{ -edible-satiated(satiated: $satiated, verb: $verb) }
edible-force-feed-success-user = Вы успешно накормили {$target}
