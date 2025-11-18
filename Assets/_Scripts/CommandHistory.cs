using System.Collections.Generic;

public static class CommandHistory
{
    private static Stack<ICommand> undoStack = new Stack<ICommand>();
    private static Stack<ICommand> redoStack = new Stack<ICommand>();
    private static int capacity = 10;

    public static void SetCapacity(int cap)
    {
        capacity = cap;
    }

    public static void AddCommand(ICommand command)
    {
        if (undoStack.Count >= capacity)
        {
            Stack<ICommand> temp = new Stack<ICommand>(new Stack<ICommand>(undoStack));
            undoStack.Clear();
            while (temp.Count > 1)
                undoStack.Push(temp.Pop());
        }

        undoStack.Push(command);
        redoStack.Clear();
    }

    public static void Undo()
    {
        if (undoStack.Count > 0)
        {
            ICommand command = undoStack.Pop();
            command.Undo();
            redoStack.Push(command);
        }
    }

    public static void Redo()
    {
        if (redoStack.Count > 0)
        {
            ICommand command = redoStack.Pop();
            command.Execute();
            undoStack.Push(command);
        }
    }

    // --- дндюмн жеи лернд ---
    public static void Clear()
    {
        undoStack.Clear();
        redoStack.Clear();
    }
}
