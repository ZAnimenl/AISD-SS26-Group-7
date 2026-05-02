# Global requirements
1. Don't use emojis in comments, commit messages
2. Use English for comments
3. Add tests to OjSharp.Tests/XxTests' incrementally, absolutely do not break project structure, do not break file structure
4. Use PostgreSQL as the database, connect with string "Host=localhost:5433;Database=ojsharp;Username=ojsharp;password=password"
5. Adhere single response principal for design, which means every class should have only one responsibility, and every method should do only one thing.
6. When need to configure a ef core class with fluent api, write a single static `XxConfiguration` class that implments static method `Configure` which takes `ModelBuilder` as parameter, and call the `Configure` method in `OnModelCreating` method of `DbContext`.

# Frontend
1. never use emoji or classical Blue gradient color in fronted UI
2. Make all frontend ui support three localization: Chinese, English and German, and the default language is English, the user can switch language in the top right corner of the page, and the language preference is stored in local storage and will be applied on next visit.
3. Use flat design, don't use big rounded corners in frontend UI, the border radius should be no more than 4px.
4. Put page UI in separate files