using Microsoft.EntityFrameworkCore;
using StudyFlow.Infrastructure.DbContexts;
using StudyFlow.Domain.Entities;

namespace StudyFlow.API.Seed
{
    public static class DataSeeder
    {
        public static async Task SeedDataAsync(StudyFlowDbContext context)
        {
            // ===== Modern Academy =====
            var modernAcademy = await context.Universities
                .FirstOrDefaultAsync(u => u.Name == "Modern Academy In Maadi");

            if (modernAcademy == null)
            {
                modernAcademy = new University
                {
                    Name = "Modern Academy In Maadi",
                    Description = "Faculty Of Computer Science"
                };

                context.Universities.Add(modernAcademy);
                await context.SaveChangesAsync();
            }

            // ===== Ain Shams =====
            var ainShams = await context.Universities
                .FirstOrDefaultAsync(u => u.Name == "Ain Shams University");

            if (ainShams == null)
            {
                ainShams = new University
                {
                    Name = "Ain Shams University",
                    Description = "Faculty of Computer and Information Sciences"
                };

                context.Universities.Add(ainShams);
                await context.SaveChangesAsync();
            }

            // ===== Subjects for Modern Academy (مطابق للصورة بالظبط) =====
            if (!context.Subjects.Any(s => s.UniversityId == modernAcademy.Id))
            {
                var modernSubjects = new List<Subject>
                {
                    new Subject
                    {
                        Name = "Parallel Processing",
                        Description = "Technologies that use multiple processors to combine computing tasks simultaneously, resulting in faster and higher speeds.",
                        UniversityId = modernAcademy.Id,
                        DoctorId = "TEMP_DOCTOR_ID"
                    },
                    new Subject
                    {
                        Name = "Internet Of Things",
                        Description = "Connecting everyday physical devices such as sensors and household appliances to the internet to collect data and enable intelligent interaction.",
                        UniversityId = modernAcademy.Id,
                        DoctorId = "TEMP_DOCTOR_ID"
                    },
                    new Subject
                    {
                        Name = "Cloud Computing",
                        Description = "Providing computing services and resources such as storage and software over the internet, making them available on demand without managing the infrastructure.",
                        UniversityId = modernAcademy.Id,
                        DoctorId = "TEMP_DOCTOR_ID"
                    },
                    new Subject
                    {
                        Name = "Data Communication",
                        Description = "Principles and protocols that govern the transfer and exchange of data between computers across various communication media and networks.",
                        UniversityId = modernAcademy.Id,
                        DoctorId = "TEMP_DOCTOR_ID"
                    }
                };

                context.Subjects.AddRange(modernSubjects);
                await context.SaveChangesAsync();
            }

            // ===== Subjects for Ain Shams =====
            if (!context.Subjects.Any(s => s.UniversityId == ainShams.Id))
            {
                var ainShamsSubjects = new List<Subject>
                {
                    new Subject
                    {
                        Name = "Artificial Intelligence",
                        Description = "Introduction to intelligent systems, machine learning algorithms, and problem-solving techniques.",
                        UniversityId = ainShams.Id,
                        DoctorId = "TEMP_DOCTOR_ID"
                    },
                    new Subject
                    {
                        Name = "Cyber Security",
                        Description = "Fundamentals of protecting systems and networks from digital attacks and vulnerabilities.",
                        UniversityId = ainShams.Id,
                        DoctorId = "TEMP_DOCTOR_ID"
                    },
                    new Subject
                    {
                        Name = "Software Engineering",
                        Description = "Concepts of software development methodologies, design patterns, and project management.",
                        UniversityId = ainShams.Id,
                        DoctorId = "TEMP_DOCTOR_ID"
                    },
                    new Subject
                    {
                        Name = "Database Systems",
                        Description = "Designing, implementing, and managing relational and non-relational database systems.",
                        UniversityId = ainShams.Id,
                        DoctorId = "TEMP_DOCTOR_ID"
                    }
                };

                context.Subjects.AddRange(ainShamsSubjects);
                await context.SaveChangesAsync();
            }
        }
    }
}