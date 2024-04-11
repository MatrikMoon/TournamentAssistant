using Fleck;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TournamentAssistantServer
{
    class Entry
    {
        public static TAServer Server;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // DrawWelcomeMessage();

            Server = new TAServer(args.Length > 0 ? args[0] : null);
            Server.Start();

            //DatabaseTester.TestDatabases();

            //Block forever
            new AutoResetEvent(false).WaitOne();
        }

        static void DrawWelcomeMessage()
        {
            Console.Clear(); // Clear the console window

            var message = " TA Server ";
            var width = Console.WindowWidth / 2; // Use full console width for the gradient effect
            var leftOffset = Console.WindowWidth / 2 / 2;
            var topOffset = 0;

            if (width < message.Length * 2)
            {
                return;
            }

            // Draw the top border with gradient
            Console.SetCursorPosition(leftOffset, topOffset);
            DrawGradientBorder(width);

            // Prepare the message line
            Console.SetCursorPosition(leftOffset, topOffset + 1);
            Console.Write("▒"); // Padding for left border alignment
            Console.ForegroundColor = ConsoleColor.Red; // Set message color to red
            Console.SetCursorPosition((leftOffset + width / 2) - (message.Length / 2), topOffset + 1);
            Console.Write(message);
            Console.SetCursorPosition(leftOffset + width - 1, topOffset + 1);
            Console.WriteLine("▒"); // Move to the next line
            Console.ResetColor();

            // Draw the bottom border with gradient
            Console.SetCursorPosition(leftOffset, topOffset + 2);
            DrawGradientBorder(width);

            Console.ResetColor(); // Reset to default color
            Console.SetCursorPosition(0, topOffset + 4); // Move cursor below the box
        }

        static void DrawGradientBorder(int width)
        {
            // Define gradient steps - simulate from red, to white, back to red
            ConsoleColor[] gradientColors = new[] { ConsoleColor.Red, ConsoleColor.White, ConsoleColor.Red };

            int sectionWidth = width / gradientColors.Length; // Divide width by the number of colors for even sections

            for (int colorIndex = 0; colorIndex < gradientColors.Length; colorIndex++)
            {
                Console.ForegroundColor = gradientColors[colorIndex];
                for (int i = 0; i < sectionWidth; i++)
                {
                    Console.Write("▒");
                }
            }

            // Fill in any remaining space with the last color
            Console.ForegroundColor = gradientColors[^1]; // ^1 is the last element
            int remainingWidth = width % gradientColors.Length;
            Console.Write(new string('▒', remainingWidth));
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var exception = (Exception)e.Exception;
            Console.WriteLine("Unobserved Task Exception Handler caught : " + exception.Message);
            Console.WriteLine("Stack Trace: " + exception.StackTrace);
        }

        private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception)e.ExceptionObject;
            Console.WriteLine("Unhandled Exception Handler caught : " + exception.Message);
            Console.WriteLine("Stack Trace: " + exception.StackTrace);
            Console.WriteLine("Runtime terminating: {0}", e.IsTerminating);
        }
    }
}
