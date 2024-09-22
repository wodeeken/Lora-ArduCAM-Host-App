namespace LoraArduCAMHostApp
{
    public class ConsolePrinter
    {
        public enum CurrentConsoleState{
            Idle,       // Only prints header.
            BlockingProgress   // Prints header with updating body, with menu greyed visibily disabled.  
        }
        private string ImageWritePath;
        public ConsolePrinter(string ImageWritePath){
            this.ImageWritePath = ImageWritePath;
        }   
        public void PrintState(CurrentConsoleState currentState){
            // If we are in a blocking state, don't print the menu.
            if(currentState == CurrentConsoleState.BlockingProgress){
                // Print header, then print Program.ProgressMessage
                PrintHeader(false);
                Console.WriteLine(Program.ProgressMessage);
            }
            else
                PrintHeader(true);
        }   
        /// <summary>
        /// Method prints header.
        /// </summary>
        private void PrintHeader(bool menuEnabled){
            Console.Clear();
            Console.BackgroundColor = ConsoleColor.DarkGreen;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Lora ArduCAM Host Application");
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine($"Images written to: {ImageWritePath}");
            PrintMenu(menuEnabled);
            string currentStateString = "";
            ConsoleColor backgroundColor;
            switch(Program.CurrentCameraState){
                case Constants.HostAppState.ReceiverDisconnected:
                    currentStateString = "Connecting to Receiver";
                    backgroundColor = ConsoleColor.Blue;
                break;
                case Constants.HostAppState.ReceiverDisconnected_WaitingForRetry:
                    currentStateString = "Disconnected from Receiver, Please Plug it in!";
                    backgroundColor = ConsoleColor.Red;
                break;
                case Constants.HostAppState.PingCamera:
                    currentStateString  = "Pinging Camera";
                    backgroundColor = ConsoleColor.Blue;
                break;
                case Constants.HostAppState.Trigger:
                    currentStateString = "Triggering Camera";
                    backgroundColor = ConsoleColor.Magenta;
                    break;
                case Constants.HostAppState.PacketTransfer:
                    currentStateString = "Transfering Camera Data";
                    backgroundColor = ConsoleColor.DarkYellow;
                    break;
                default:
                    currentStateString = "Connected to Receiver and Waiting For Command!";
                    backgroundColor = ConsoleColor.Green;
                break;
            }
            Console.ResetColor();

            Console.Write("Current State:" );
            Console.BackgroundColor = backgroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(currentStateString);
            Console.ResetColor();
        }
        private void PrintMenu(bool isEnabled){
            if(isEnabled){
                Console.BackgroundColor = ConsoleColor.Green;
                Console.Write("Commands: ");
            }else{
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.Write("CMD UNAVLBLE: ");
            }
            
            Console.WriteLine("Q - Quit     T - Take Picture    P - Ping Camera     ");
            Console.ResetColor();
        }

    }
}