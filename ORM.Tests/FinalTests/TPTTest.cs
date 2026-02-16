using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ORM_v1.Attributes;
using ORM_v1.Configuration;
using ORM_v1.core;
using ORM_v1.Mapping;
using ORM_v1.Mapping.Strategies;
using ORM_v1.Query;
using System.Threading.Tasks;
namespace ORM.Tests.FinalTests
{
    public class TPTTest
    {
        #region TPT MODELS
        [Table("People")]
        [InheritanceStrategy(InheritanceStrategy.TablePerType)]
        public abstract class Person
        {
            [Key]
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [Table("Employees")]
        public class Employee : Person
        {
            public string Title { get; set; }
            public List<Subject> Subjects { get; set; } //One To Many
        }

        [Table("Students")]
        public class Student : Person
        {
            public string Major { get; set; }
            public List<StudentSubject> StudentSubjects { get; set; } //Many To Many
            public List<Subject> Subjects { get { return StudentSubjects?.Select(x => x.Subject)?.ToList() ?? new List<Subject>(); } }
        }

        [Table("Subjects")]
        public class Subject
        {
            [Key]
            public int Id { get; set; }
            public string Name { get; set; }
            public List<StudentSubject> StudentSubjects { get; set; } //Many To Many
            public int EmployeeId { get; set; }

            [ForeignKey("EmployeeId")]
            public Employee Employee { get; set; } // Many To One
            public List<Student> Students { get { return StudentSubjects?.Select(x => x.Student)?.ToList() ?? new List<Student>(); } }
        }

        [Table("StudentSubjects")]
        public class StudentSubject
        {
            [Key]
            public int Id { get; set; }
            public int StudentId { get; set; }

            [ForeignKey("StudentId")]
            public Student Student { get; set; }

            public int SubjectId { get; set; }

            [ForeignKey("SubjectId")]
            public Subject Subject { get; set; }
        }
        #endregion

        #region Tests
        [Fact]
        public void TPT_Test()
       {
            var dbConfig = FinalTestHelper.BuildDb("TPT_Test.db",
                typeof(Person),
                typeof(Employee),
                typeof(Student),
                typeof(Subject),
                typeof(StudentSubject));

            using (var context = new DbContext(dbConfig))
            {
                //Arrange
                var emp1 = new Employee { Name = "Alice", Title = "Dr" };
                var emp2 = new Employee { Name = "Bob", Title = "Prof" };

                var stu1 = new Student { Name = "Charlie", Major = "CS" };
                var stu2 = new Student { Name = "Diana", Major = "Math" };
                var stu3 = new Student { Name = "Eve", Major = "CS" };

                context.Set<Employee>().Add(emp1);
                context.Set<Employee>().Add(emp2);
                context.Set<Student>().Add(stu1);
                context.Set<Student>().Add(stu2);
                context.Set<Student>().Add(stu3);
                context.SaveChanges();
            }

            //Insert
            using (var context = new DbContext(dbConfig))
            {
                var employees = context.Set<Employee>().ToList();
                var students = context.Set<Student>().ToList();
                var people = context.Set<Person>().ToList();
                var studentsDuplicate = context.Set<Student>().ToList();
                var studentWhere = context.Set<Student>().Where(s => s.Name == "Eve").ToList();

                Assert.Equal(5, context.ChangeTracker.Entries.Count());
                Assert.Equal(2, employees.Count);
                Assert.Equal(3, students.Count);
                Assert.Equal(1, studentWhere.Count);

                var empl1 = employees.FirstOrDefault(e => e.Name == "Alice");
                Assert.NotNull(empl1?.Id);

                var empl2 = employees.FirstOrDefault(e => e.Name == "Bob");
                Assert.NotNull(empl2?.Id);

                var stu1 = students.FirstOrDefault(s => s.Name == "Charlie");
                Assert.NotNull(stu1?.Id);

                var stu2 = students.FirstOrDefault(s => s.Name == "Diana");
                Assert.NotNull(stu2?.Id);

                var stu3 = studentWhere.FirstOrDefault();
                Assert.NotNull(stu3?.Id);

                var sub = new Subject { Name = "Databases", EmployeeId = empl1.Id };
                var sub2 = new Subject { Name = "Algorithms", EmployeeId = empl2.Id };

                context.Set<Subject>().Add(sub);
                context.Set<Subject>().Add(sub2);
                context.SaveChanges();
            }

            //Insert
            using (var context = new DbContext(dbConfig))
            {
                var subjects = context.Set<Subject>().ToList();
                Assert.Equal(2, subjects.Count);

                var sub1 = subjects.FirstOrDefault(s => s.Name == "Databases");
                Assert.NotNull(sub1?.Id);

                var sub2 = subjects.FirstOrDefault(s => s.Name == "Algorithms");
                Assert.NotNull(sub2?.Id);

                var students = context.Set<Student>().ToList();
                var studentsDuplicate = context.Set<Student>().ToList();
                var studentWhere = context.Set<Student>().Where(s => s.Name == "Eve").ToList();

                var stu1 = students.FirstOrDefault(s => s.Name == "Charlie");
                Assert.NotNull(stu1?.Id);

                var stu2 = students.FirstOrDefault(s => s.Name == "Diana");
                Assert.NotNull(stu2?.Id);

                var stu3 = studentWhere.FirstOrDefault();
                Assert.NotNull(stu3?.Id);

                context.Set<StudentSubject>().Add(new StudentSubject { StudentId = stu1.Id, SubjectId = sub1.Id });
                context.Set<StudentSubject>().Add(new StudentSubject { StudentId = stu1.Id, SubjectId = sub2.Id });
                context.Set<StudentSubject>().Add(new StudentSubject { StudentId = stu2.Id, SubjectId = sub1.Id });
                context.Set<StudentSubject>().Add(new StudentSubject { StudentId = stu3.Id, SubjectId = sub1.Id });
                context.Set<StudentSubject>().Add(new StudentSubject { StudentId = stu3.Id, SubjectId = sub2.Id });
                context.SaveChanges();
            }

            //Update From Navigation
            using (var context = new DbContext(dbConfig))
            {
                var students = context.Set<Student>()
                    .Include(s => s.StudentSubjects).ThenInclude(x => x.Subject)
                    .ToList()
                    .OrderByDescending(x => x.Name)
                    .ToList();

                foreach (var student in students)
                {
                    Assert.True(student.StudentSubjects.Count > 0);
                    Assert.True(student.Subjects.Count > 0);
                }

                var studentCharlie = students.First();
                Assert.Equal("Eve", studentCharlie.Name);

                var subjectDb = studentCharlie.Subjects.FirstOrDefault(s => s.Name == "Databases");
                Assert.NotNull(subjectDb);

                subjectDb.Name = "Advanced Databases";
                foreach (var s in students)
                {
                    foreach (var ss in s.StudentSubjects)
                    {
                        if (ss.SubjectId == subjectDb.Id)
                        {
                            Assert.Equal("Advanced Databases", ss.Subject.Name);
                        }
                        Assert.False(ss.Subject.Name == "Databases");
                    }
                }
                context.Set<Subject>().Update(subjectDb);
                context.SaveChanges();
            }

            //Delete From Navigation
            using (var context = new DbContext(dbConfig))
            {
                var subjectDb = context.Set<Subject>().Where(x => x.Name == "Advanced Databases")
                    .Include(x => x.StudentSubjects).ThenInclude(y => y.Student)
                    .ToList()
                    .FirstOrDefault();

                Assert.NotNull(subjectDb);
                Assert.Equal("Advanced Databases", subjectDb.Name);

                var ss = subjectDb.StudentSubjects.FirstOrDefault();
                Assert.NotNull(ss);

                context.Set<StudentSubject>().Remove(ss);
                context.SaveChanges();

                var ssAfter = context.Set<StudentSubject>().ToList();
                var ssAfter2 = context.Set<StudentSubject>().All();

                Assert.False(ssAfter.Any(x => x.Id == ss.Id));
                Assert.Equal(4, ssAfter.Count());
                Assert.Equal(4, ssAfter2.Count());
            }

            //Update From Parent Table
            using (var context = new DbContext(dbConfig))
            {
                var people = context.Set<Person>().Where(x => x.Name == "Bob").ToList();
                Assert.Single(people);
                var bobPerson = context.Set<Person>().Find(people.First().Id);
                Assert.NotNull(bobPerson);
                Assert.Equal(1, context.ChangeTracker.Entries.Count());

                bobPerson.Name = "Bobby";
                Assert.Equal(people.First().Id, bobPerson.Id);
                Assert.Equal(people.First().Name, bobPerson.Name);
                context.Set<Person>().Update(bobPerson);
                context.SaveChanges();

                var bobEmployee = context.Set<Employee>().Find(bobPerson.Id);
                Assert.NotNull(bobEmployee);
                Assert.Equal("Bobby", bobEmployee.Name);
            }

            //Summary
            using (var context = new DbContext(dbConfig))
            {
                var people = context.Set<Person>().ToList();
                Assert.Equal(5, people.Count);

                var employees = context.Set<Employee>().ToList();
                Assert.Equal(2, employees.Count);

                var students = context.Set<Student>().ToList();
                Assert.Equal(3, students.Count);

                var subjects = context.Set<Subject>().ToList();
                Assert.Equal(2, subjects.Count);

                var studentSubjects = context.Set<StudentSubject>().ToList();
                Assert.Equal(4, studentSubjects.Count);
            }
        }
        #endregion
    }
}
