using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Navigation is scoped to the visible page so automatic navigation cannot reach controls
// behind a modal panel. Input fields remain available to mouse and keyboard.
public static class GamepadNavigation
{
    private static bool Eligible(Selectable item)
        => item != null && item.gameObject.activeInHierarchy && item.IsInteractable() && !(item is InputField) && !(item is TMPro.TMP_InputField);

    public static void Configure(GameObject page)
    {
        if (page == null || !page.activeInHierarchy) return;
        var controls = page.GetComponentsInChildren<Selectable>(false);
        foreach (var item in controls)
        {
            var navigation = item.navigation;
            if (item is InputField || item is TMPro.TMP_InputField)
            {
                navigation.mode = Navigation.Mode.None;
            }
            else
            {
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = Find(controls, item, Vector2.up);
                navigation.selectOnDown = Find(controls, item, Vector2.down);
                navigation.selectOnLeft = Find(controls, item, Vector2.left);
                navigation.selectOnRight = Find(controls, item, Vector2.right);
            }
            item.navigation = navigation;
        }
    }

    private static Selectable Find(Selectable[] controls, Selectable source, Vector2 direction)
    {
        Selectable best = null;
        float bestScore = 0f;
        Vector3 origin = source.transform.position;
        foreach (var candidate in controls)
        {
            if (candidate == source || !Eligible(candidate)) continue;
            Vector3 difference = candidate.transform.position - origin;
            float projection = difference.x * direction.x + difference.y * direction.y;
            float distance = difference.x * difference.x + difference.y * difference.y;
            if (projection <= 0.001f || distance <= 0.000001f) continue;
            float score = projection / distance;
            if (score > bestScore) { bestScore = score; best = candidate; }
        }
        return best;
    }

    public static void EnsureFocus(GameObject page, bool usingGamepad)
    {
        var events = EventSystem.current;
        if (!usingGamepad || !Application.isFocused || events == null || page == null || !page.activeInHierarchy) return;
        var selected = events.currentSelectedGameObject;
        var control = selected != null ? selected.GetComponent<Selectable>() : null;
        if (Eligible(control) && selected.transform.IsChildOf(page.transform)) return;
        foreach (var candidate in page.GetComponentsInChildren<Selectable>(false))
            if (Eligible(candidate)) { events.SetSelectedGameObject(candidate.gameObject); return; }
        events.SetSelectedGameObject(null);
    }
}
