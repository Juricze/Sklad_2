using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Sklad_2.Messages
{
    public class ShowDepositConfirmationMessage : ValueChangedMessage<decimal>
    {
        public ShowDepositConfirmationMessage(decimal value) : base(value)
        {
        }
    }
}
