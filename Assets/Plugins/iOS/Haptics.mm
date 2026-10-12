// Tiny bridge to iOS haptic feedback, called from Haptics.cs.
#import <UIKit/UIKit.h>

extern "C" {

void _HapticImpact(int style)
{
    // 0 = light, 1 = medium, 2 = heavy
    UIImpactFeedbackStyle s = style == 0 ? UIImpactFeedbackStyleLight : style == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
    [g impactOccurred];
}

void _HapticNotify(int type)
{
    // 0 = success, 1 = warning, 2 = error
    UINotificationFeedbackType t = type == 0 ? UINotificationFeedbackTypeSuccess : type == 1 ? UINotificationFeedbackTypeWarning : UINotificationFeedbackTypeError;
    UINotificationFeedbackGenerator *g = [[UINotificationFeedbackGenerator alloc] init];
    [g notificationOccurred:t];
}

void _HapticSelection()
{
    UISelectionFeedbackGenerator *g = [[UISelectionFeedbackGenerator alloc] init];
    [g selectionChanged];
}

}
