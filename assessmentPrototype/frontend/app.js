const todos = [
  { id: 1, title: "Write public tests", description: "Cover the visible Todo contract", completed: true },
  { id: 2, title: "Review sandbox output", description: "Check the interactive preview", completed: false },
];

const list = document.querySelector("#todo-list");
const form = document.querySelector("#todo-form");
const title = document.querySelector("#todo-title");
const description = document.querySelector("#todo-description");

function renderChart(completed, pending) {
  const canvas = document.querySelector("#completion-chart");
  const context = canvas.getContext("2d");
  const total = Math.max(1, completed + pending);
  context.clearRect(0, 0, canvas.width, canvas.height);
  context.fillStyle = "#dbeafe";
  context.fillRect(20, 42, canvas.width - 40, 36);
  context.fillStyle = "#2563eb";
  context.fillRect(20, 42, (canvas.width - 40) * (completed / total), 36);
  context.fillStyle = "#1e3a8a";
  context.font = "16px Segoe UI";
  context.fillText(`${completed} of ${completed + pending} completed`, 20, 28);
}

function render() {
  list.replaceChildren();
  for (const todo of todos) {
    const item = document.createElement("li");
    item.className = todo.completed ? "completed" : "";
    item.innerHTML = `
      <input type="checkbox" ${todo.completed ? "checked" : ""} aria-label="Toggle ${todo.title}">
      <div><strong></strong><p></p></div>
      <button class="delete" type="button">Delete</button>`;
    item.querySelector("strong").textContent = todo.title;
    item.querySelector("p").textContent = todo.description;
    item.querySelector("input").addEventListener("change", () => {
      todo.completed = !todo.completed;
      render();
    });
    item.querySelector(".delete").addEventListener("click", () => {
      todos.splice(todos.indexOf(todo), 1);
      render();
    });
    list.append(item);
  }
  const completed = todos.filter((todo) => todo.completed).length;
  const pending = todos.length - completed;
  document.querySelector("#pending-count").textContent = `${pending} pending`;
  document.querySelector("#completed-count").textContent = `${completed} done`;
  document.querySelector("#empty-state").hidden = todos.length > 0;
  renderChart(completed, pending);
}

form.addEventListener("submit", (event) => {
  event.preventDefault();
  if (!title.value.trim()) return;
  todos.push({ id: Date.now(), title: title.value.trim(), description: description.value.trim(), completed: false });
  form.reset();
  render();
});

render();

