
using Microsoft.AspNetCore.Mvc.Rendering;
using ToDo.Domain;

public enum TaskSort
{
    DueSoonest,     // En yakın son tarih
    PriorityHigh,   // En büyük öncelik
    TitleAZ,        // A -> Z
    TitleZA         // Z -> A
}

public class TaskListVm
{
    public IEnumerable<TodoItem> Items { get; set; } = Enumerable.Empty<TodoItem>();
    public TaskSort Sort { get; set; } = TaskSort.DueSoonest;
    public IEnumerable<SelectListItem> SortOptions =>
        new[]
        {
            new SelectListItem("En yakın son tarih", TaskSort.DueSoonest.ToString(), Sort == TaskSort.DueSoonest),
            new SelectListItem("En büyük öncelik",   TaskSort.PriorityHigh.ToString(), Sort == TaskSort.PriorityHigh),
            new SelectListItem("Başlık A'dan Z'ye",  TaskSort.TitleAZ.ToString(),      Sort == TaskSort.TitleAZ),
            new SelectListItem("Başlık Z'den A'ya",  TaskSort.TitleZA.ToString(),      Sort == TaskSort.TitleZA),
        };
}
