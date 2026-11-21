---
trigger: always_on
---

# Rule: использование `EntityQuery`

Эта норма обязательна при создании и рефакторинге C#-кода SS14.

## 1. Различать API

Не смешивать три разных механизма:

1. `EntityQuery<TComponent>` — сохранённый типизированный доступ к хранилищу одного компонента.
2. `EntityQueryEnumerator<T...>()` — перечисление сущностей с заданными компонентами.
3. `EntityQuery<T...>()` и `AllEntityQuery<T...>()` — перечисление сущностей, а не dependency-поле.

Правило dependency ниже относится только к `EntityQuery<TComponent>`.

## 2. Внутри `EntitySystem`

В `EntitySystem`, его наследниках, игровых правилах-системах и их partial-файлах получать
`EntityQuery<TComponent>` через системную коллекцию зависимостей:

```csharp
[Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
```

Не присваивать такое поле через `GetEntityQuery<TComponent>()` в `Initialize()`. Перед добавлением
поля проверить все partial-файлы системы и переиспользовать уже существующий query того же типа.

## 3. Вне `EntitySystem`

Ленивая регистрация `EntityQuery<>` находится в `EntitySystemManager.SystemDependencyCollection`,
а не в глобальной IoC-коллекции. Поэтому запрещено добавлять `[Dependency] EntityQuery<T>` в
`IConsoleCommand`, HTN-операторы, UI-контролы, оверлеи и произвольные helper-классы, которые
инъектируются или создаются вне менеджера систем.

Выбирать решение по контексту:

1. Для единичной проверки использовать `IEntityManager.TryGetComponent`, `HasComponent` или
   `GetComponent`.
2. Для часто используемого query получить его через `IEntityManager.GetEntityQuery<T>()` в
   контролируемом месте создания объекта.
3. Для объекта, создаваемого системой, передать готовый `EntityQuery<T>` через конструктор.

## 4. Запрет механического рефакторинга

Не заменять по строковому совпадению `EntityQueryEnumerator`, `EntityQuery<T...>()` или
`AllEntityQuery<T...>()`. После массового изменения проверить тип каждого содержащего класса и
убедиться, что dependency-поля находятся только в системах.

