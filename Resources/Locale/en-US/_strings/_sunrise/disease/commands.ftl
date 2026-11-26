## Common

cmd-disease-unknown-disease = Unknown disease { $disease }.
cmd-disease-not-infected-with = The entity does not have { $disease }. Use the strain code from diseaselist.
cmd-disease-hint-disease = Strain code or prototype ID
cmd-disease-hint-stage = Stage number (starting from 1)

## diseaseinfect

cmd-diseaseinfect-desc = Infects an entity with a disease, bypassing transmission routes, protection and immunity.
cmd-diseaseinfect-help = Usage: { $command } <uid> <disease>
cmd-diseaseinfect-success = The entity is now infected with { $disease }.
cmd-diseaseinfect-failed = Infection failed: the entity already has { $disease } or the disease is invalid.

## diseasecure

cmd-diseasecure-desc = Cures an entity of one or every disease without granting immunity.
cmd-diseasecure-help = Usage: { $command } <uid> [disease]
cmd-diseasecure-success = Cured { $disease }.
cmd-diseasecure-all-success = Cured every disease.
cmd-diseasecure-not-infected = The entity does not have this disease.

## diseasesetstage

cmd-diseasesetstage-desc = Moves a disease of an entity to the given stage.
cmd-diseasesetstage-help = Usage: { $command } <uid> <disease> <stage starting from 1>
cmd-diseasesetstage-success = Moved { $disease } to stage { $stage }.
cmd-diseasesetstage-failed = Stage change failed: the entity does not have { $disease } or the stage is outside 1-{ $count }.

## diseaselist

cmd-diseaselist-desc = Shows the diseases and immunities of an entity.
cmd-diseaselist-help = Usage: { $command } <uid>
cmd-diseaselist-none = The entity has no diseases.
cmd-diseaselist-entry = { $disease } [{ $strain }]: stage { $stage }/{ $count } ({ $elapsed } s){ $recovering ->
        [true] , recovering
       *[other] {""}
    }, suppression { $suppression } s, cure { $cure }/{ $threshold }
cmd-diseaselist-symptoms = - symptoms (onset stage): { $symptoms }
cmd-diseaselist-research = - strength { $strength }, research { $research }/{ $goal }, recipe: { $solved }
cmd-diseaselist-symptoms-none = - no symptoms
cmd-diseaselist-immunity-permanent = Immune to { $disease }: permanent
cmd-diseaselist-immunity-temporary = Immune to { $disease }: { $remaining } s left

## diseaseoverlay

cmd-diseaseoverlay-desc = Toggles the disease overlay: contaminated items, clouds and carriers with their diseases and stages.
cmd-diseaseoverlay-help = Usage: { $command }
cmd-diseaseoverlay-enabled = Disease overlay enabled.
cmd-diseaseoverlay-disabled = Disease overlay disabled.
