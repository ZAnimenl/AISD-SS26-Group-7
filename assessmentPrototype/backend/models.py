from peewee import AutoField, BooleanField, CharField, Model, SqliteDatabase, TextField

try:
    from config.environment import DATABASE_PATH
except ModuleNotFoundError:
    from environment import DATABASE_PATH

db = SqliteDatabase(DATABASE_PATH)


class Todo(Model):
    id = AutoField()
    title = CharField()
    description = TextField(default="")
    completed = BooleanField(default=False)

    class Meta:
        database = db
