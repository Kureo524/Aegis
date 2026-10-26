using System.Collections.Generic;
using Features.Economy;

namespace Features.Characters {
    /// <summary>
    /// A view drawn over a character (the HP bar, Features.UI), found in the prefab's children:
    /// the character only knows this interface, so the gameplay never depends on the presentation.
    /// </summary>
    public interface ICharacterOverlay {
        void Bind(Character character);
        void ShowItems(IReadOnlyList<ItemDefinition> items, bool elite);
    }
}
