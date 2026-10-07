# Postman

Import both files, then select the environment before sending requests.

- Collection: [postman/Customer API.postman.json](postman/Customer%20API.postman.json)
- Environment: [postman/Develop Env.postman.json](postman/Develop%20Env.postman.json)

In Postman: **Import**, choose those two files, and set the active environment to **Develop Env**.

`baseUrl` is `http://localhost:5081/api`. The collection calls `{{baseUrl}}/customers`, so the requests land on `/api/customers`. Change `baseUrl` if your `API_PORT` is different. A local `dotnet run` uses `http://localhost:5080/api`.
