using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Sklad_2.Messages
{
    public class RoleChangedMessage : ValueChangedMessage<string>
    {
        public RoleChangedMessage(string role) : base(role)
        {
        }
    }
}