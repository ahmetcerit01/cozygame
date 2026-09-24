// Minimal UIKit haptics bridge for CozyLab (called from Haptics.cs via DllImport("__Internal")).
// Generators are created once and kept alive; Unity calls these on the main thread.
#import <UIKit/UIKit.h>

static UISelectionFeedbackGenerator* s_selection = nil;
static UINotificationFeedbackGenerator* s_notification = nil;
static UIImpactFeedbackGenerator* s_impact[5] = { nil, nil, nil, nil, nil };

static UIImpactFeedbackStyle CozyImpactStyle(int style)
{
    switch (style)
    {
        case 1: return UIImpactFeedbackStyleMedium;
        case 2: return UIImpactFeedbackStyleHeavy;
        case 3: return UIImpactFeedbackStyleSoft;
        case 4: return UIImpactFeedbackStyleRigid;
        default: return UIImpactFeedbackStyleLight;
    }
}

extern "C"
{
    void _CozyHapticsSelection()
    {
        if (s_selection == nil) s_selection = [[UISelectionFeedbackGenerator alloc] init];
        [s_selection selectionChanged];
        [s_selection prepare];
    }

    void _CozyHapticsImpact(int style)
    {
        if (style < 0 || style > 4) style = 0;
        if (s_impact[style] == nil) s_impact[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:CozyImpactStyle(style)];
        [s_impact[style] impactOccurred];
        [s_impact[style] prepare];
    }

    void _CozyHapticsNotify(int type)
    {
        if (s_notification == nil) s_notification = [[UINotificationFeedbackGenerator alloc] init];
        UINotificationFeedbackType feedback = UINotificationFeedbackTypeSuccess;
        if (type == 1) feedback = UINotificationFeedbackTypeWarning;
        else if (type == 2) feedback = UINotificationFeedbackTypeError;
        [s_notification notificationOccurred:feedback];
    }
}
