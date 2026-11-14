---
name: framework-naming
description: >-
  Chooses framework-grade names for types, members, enums, events, properties, CSS
  pseudo-classes, data attributes, and template parts. Use this skill before naming
  anything new, when renaming, when proposing an API in a design, and when reviewing
  code or plans for names that are invented, vague, or sentence-like.
---

# Framework-Grade Naming

Every name in NativeUI must read like the API of an established UI framework. A name is
framework grade when a mature framework uses it for the same concept, or when it is built
only from vocabulary those frameworks use. A name invented to describe the current
situation is not framework grade.

## 1. Find the established name first

Before naming a concept, look up what established frameworks call it.

| Concept area | Sources, in order of preference |
| --- | --- |
| Types, members, events, enums, attached properties | WPF, WinUI, Avalonia, .NET MAUI |
| Native windowing, input, accessibility | AppKit, UIKit, Android, Win32 |
| CSS properties, pseudo-classes, selectors | CSS specifications and browser behaviour |
| Element attributes, roles, states | HTML, the DOM, WAI-ARIA |
| Headless control parts, `data-*` state attributes | Base UI, Radix |

1. State the concept in one short noun phrase, such as "the side a popup opens on".
2. Find the name in at least two sources from the table.
3. Take the .NET XAML framework name for C# API. Take the web standard name for CSS and
   attribute names.
4. When the sources disagree, prefer the name that more of them share, then the one
   closest to WPF or WinUI.
5. When no framework has the concept, compose the name from words those frameworks
   already use, and from names that already exist in this repository.

## 2. Say where a name comes from

- In a design, a plan, or an answer that proposes a new name, state its source, such as
  "WinUI" or "Base UI".
- Mark a name that no framework uses as new, and say which existing words it is built from.
- Never present an invented name as an established one. When a name was invented
  earlier, say so and replace it.
- When several candidates fit, list them with their sources and ask before choosing.

## 3. Shapes of good names

| Symbol | Shape | Examples of the shape |
| --- | --- | --- |
| Type | Noun or noun phrase | `ScrollViewer`, `DataTemplate`, `CornerRadius` |
| Interface | `I` + capability or role | `ICommand`, `IScrollInfo` |
| Method | Verb, or verb + noun | `Show`, `Close`, `Measure`, `Arrange`, `InvalidateArrange` |
| Boolean property | `Is`, `Has`, or `Can` + adjective or noun | `IsEnabled`, `HasItems`, `CanExecute` |
| Event | Past tense, or a `-ing`/past pair | `Click`, `Opened`/`Closed`, `Closing`/`Closed` |
| Event arguments | Event name + `EventArgs` | `SelectionChangedEventArgs` |
| Enum | Singular noun, members as nouns or adjectives | `Orientation.Horizontal`, `Visibility.Collapsed` |
| Attached property | `Get`/`Set` + property name on the owner type | `Grid.GetRow`, `Grid.SetRow` |
| Bindable property field | Property name + `Property` | `IsOpenProperty` |
| Try pattern | `Try` + verb, returning `bool` with an `out` value | `TryParse`, `TryGetValue` |
| CSS pseudo-class | The CSS or browser name | `:hover`, `:focus-visible`, `:popover-open` |
| `data-*` attribute | The Base UI or Radix name | `data-open`, `data-side`, `data-orientation` |
| Template part | Short lowercase noun | `track`, `thumb`, `content`, `icon` |

## 4. Names to reject

| Reject | Why | Replace with |
| --- | --- | --- |
| Adverb qualifiers: `Effectively`, `Actually`, `Really`, `Currently`, `Already` | They describe the code path, not the concept | The plain concept name, with detail in the doc comment |
| Sentence names: `DoXWhenY`, `GetXForZ`, `IsXAfterY` | They spell out a situation | The established name of the concept |
| Vague verbs: `Report`, `Process`, `Manage`, `Do`, `Perform`, `Execute` without a command | They say nothing about the effect | The verb a framework uses for that effect |
| Filler suffixes: `Helper`, `Util`, `Info`, `Data`, `Impl`, `Realizer`, `Thing` | They hide what the type is | The framework type name, or a noun for its responsibility |
| Redundant prefixes or platform words in shared names: `UI`, `Native`, `Platform`, `MacOS` | Namespaces and assemblies carry that | The name without the prefix |
| Abbreviations and invented acronyms | They are not searchable or obvious | Whole words, except established ones such as `Id`, `Uri`, `Xml` |
| Boolean parameters that switch behaviour | The call site cannot be read | Two methods, or an enum with framework-grade members |
| A `Manager`, `Controller`, or `Service` suffix the frameworks do not use for that concept | It hides the responsibility | The concept name that the frameworks use |

## 5. Internal and private names

- Internal and private names meet the same bar. A reader of the source should recognise
  them as easily as public ones.
- Internal callbacks from platform hosts follow the conventions already in the repository
  for that layer. Search for the nearest similar member and match its pattern.
- Private fields and locals name what they hold, as nouns, never how they were computed.

## Verification

1. For every new or renamed symbol, name the framework source, or state that the name is
   new and which existing words it is built from.
2. Confirm no rejected shape from section 4 appears.
3. Confirm the name matches the shape in section 3 for its kind of symbol.
4. Confirm the same concept has the same name everywhere it appears: C#, CSS, `data-*`
   attributes, Web proxies, and documentation.
