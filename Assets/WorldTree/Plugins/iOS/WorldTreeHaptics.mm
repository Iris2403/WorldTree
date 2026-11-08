// WORLD TREE haptics for iOS (GDD §3.6). Called from Haptics.cs through DllImport("__Internal").
// kind: 0 light impact, 1 medium impact, 2 success notification, 3 warning notification.
#import <UIKit/UIKit.h>

extern "C" void WT_Haptic(int kind)
{
    if (@available(iOS 10.0, *))
    {
        switch (kind)
        {
            case 0:
            {
                UIImpactFeedbackGenerator *light = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
                [light impactOccurred];
                break;
            }
            case 1:
            {
                UIImpactFeedbackGenerator *medium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
                [medium impactOccurred];
                break;
            }
            case 2:
            {
                UINotificationFeedbackGenerator *success = [[UINotificationFeedbackGenerator alloc] init];
                [success notificationOccurred:UINotificationFeedbackTypeSuccess];
                break;
            }
            default:
            {
                UINotificationFeedbackGenerator *warning = [[UINotificationFeedbackGenerator alloc] init];
                [warning notificationOccurred:UINotificationFeedbackTypeWarning];
                break;
            }
        }
    }
}
