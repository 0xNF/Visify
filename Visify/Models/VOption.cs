using Visify.Services;

namespace Visify.Models {

    public class VOption<T> {

        public readonly T Value;
        public readonly string ErrorMessage;
        public readonly int ErrorCode;
        public bool WasSuccess => ErrorCode == (int)ErrorCodes.NoError;

        public VOption() {
            this.ErrorCode = (int)ErrorCodes.NoError;
            this.ErrorMessage = "";
        }

        public VOption(T value) {
            this.Value = value;
            this.ErrorCode = (int)ErrorCodes.NoError;
            this.ErrorMessage = "";
        }

        public VOption(int errCode, string errMsg) {
            this.ErrorMessage = errMsg;
            this.ErrorCode = errCode;
        }

        public VOption(ErrorCodes errCode, string errMsg) {
            this.ErrorMessage = errMsg;
            this.ErrorCode = (int)errCode;
        }

    }
}
