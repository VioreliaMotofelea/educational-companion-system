# Seminar 4 Worksheet: SQL Joins and Aggregations

**Course:** Databases (demo) — `databases-demo-course`  
**Catalog resource:** SQL Joins Seminar Worksheet (CourseOnly)  
**Format:** Printed worksheet + optional digital upload for the Educational Companion System  
**Duration:** ~45 minutes (in-class exercises + short reflection)

---

## Course context

Seminar 4 follows the relational modeling and normalization readings from Week 3. You already know how to decompose wide tables into `Student`, `Course`, `Instructor`, and `Enrollment` relations. **Joins** are how you put those facts back together in SQL without reintroducing redundancy in storage.

This worksheet is **course-only**: learners enrolled in `databases-demo-course` may receive the printed copy in Seminar 4 and, when uploaded to the companion app, see an **extracted summary** tied to catalog resource 065. Learners outside the course scope must not see this material through recommendations or ingestion APIs.

---

## Intended learning outcomes

After completing the exercises you should be able to:

1. Write **INNER JOIN** queries that return only rows with matching keys in both tables.
2. Use **LEFT JOIN** (and recognize **RIGHT JOIN**) to include rows that lack a match on one side.
3. Combine multiple joins along a foreign-key path (enrollment → course → instructor).
4. Apply **GROUP BY** with aggregate functions (`COUNT`, `AVG`, `MAX`) and interpret results.
5. Distinguish **filtering in WHERE** from **filtering joined rows in ON** for outer joins.
6. Explain why join results depend on **cardinality** (one-to-many vs many-to-many).

**Keywords for study and semantic matching:** SQL joins, INNER JOIN, LEFT JOIN, RIGHT JOIN, GROUP BY, aggregate functions, foreign key, enrollment, relational query, join cardinality, NULL in outer joins, seminar worksheet, databases course.

---

## Reference schema (normalized)

Use this schema for all exercises unless stated otherwise.

```sql
CREATE TABLE Student (
  StudentId   INT PRIMARY KEY,
  FullName    VARCHAR(100) NOT NULL,
  Email       VARCHAR(120)
);

CREATE TABLE Course (
  CourseCode  VARCHAR(10) PRIMARY KEY,
  CourseTitle VARCHAR(120) NOT NULL,
  Credits     SMALLINT NOT NULL
);

CREATE TABLE Instructor (
  InstructorId   INT PRIMARY KEY,
  InstructorName VARCHAR(100) NOT NULL,
  Office         VARCHAR(20)
);

CREATE TABLE Enrollment (
  EnrollmentId INT PRIMARY KEY,
  StudentId    INT NOT NULL REFERENCES Student(StudentId),
  CourseCode   VARCHAR(10) NOT NULL REFERENCES Course(CourseCode),
  Semester     VARCHAR(10) NOT NULL,
  Grade        CHAR(2),
  InstructorId INT REFERENCES Instructor(InstructorId)
);
```

**Sample rows (abbreviated):**

| StudentId | FullName   |
|-----------|------------|
| 101       | Alex M.    |
| 102       | Bianca P.  |
| 103       | Catalin R. |

| CourseCode | CourseTitle              | Credits |
|------------|--------------------------|---------|
| DB101      | Introduction to Databases | 5       |
| DB201      | Relational Design         | 6       |
| WEB110     | Web Programming           | 5       |

| EnrollmentId | StudentId | CourseCode | Semester | Grade | InstructorId |
|--------------|-----------|------------|----------|-------|--------------|
| 1            | 101       | DB101      | 2025S1   | A     | 10           |
| 2            | 101       | DB201      | 2025S1   | B+    | 11           |
| 3            | 102       | DB101      | 2025S1   | A-    | 10           |
| 4            | 103       | WEB110     | 2025S1   | NULL  | NULL         |

Note: enrollment 4 has **no instructor assigned yet** — useful for outer-join exercises.

---

## Section A — INNER JOIN fundamentals

**A1.** List each student's full name together with the course title for every enrollment.

*Starter pattern:*
```sql
SELECT s.FullName, c.CourseTitle
FROM Enrollment e
INNER JOIN Student s ON e.StudentId = s.StudentId
INNER JOIN Course c ON e.CourseCode = c.CourseCode;
```

**A2.** Return only enrollments in semester `2025S1` where the course has **6 credits**. Include `StudentId`, `CourseCode`, and `Credits`.

**A3.** Why does an INNER JOIN between `Enrollment` and `Instructor` **exclude** row 4 from the sample data? Write one sentence using the words *matching key* and *NULL*.

---

## Section B — LEFT JOIN and unmatched rows

**B1.** List **all students** and, when present, the course code they enrolled in. Students without enrollments should still appear with `NULL` in the course column.

```sql
SELECT s.StudentId, s.FullName, e.CourseCode
FROM Student s
LEFT JOIN Enrollment e ON s.StudentId = e.StudentId;
```

**B2.** Add a student `104` with no enrollments to the conceptual dataset. How many rows does B1 return compared to an INNER JOIN starting from `Student`?

**B3.** List all enrollments and the instructor name when assigned. Enrollments without an instructor must still appear.

*Hint:* `Enrollment` LEFT JOIN `Instructor` on `InstructorId`.

**B4.** **Common mistake:** placing `WHERE InstructorName = 'Dr. Pop'` after a LEFT JOIN. Explain why this can silently turn the query into an inner join effect.

---

## Section C — Multi-table paths and GROUP BY

**C1.** For each course, count how many enrollments exist in `2025S1`.

```sql
SELECT c.CourseCode, c.CourseTitle, COUNT(e.EnrollmentId) AS EnrollmentCount
FROM Course c
LEFT JOIN Enrollment e
  ON c.CourseCode = e.CourseCode AND e.Semester = '2025S1'
GROUP BY c.CourseCode, c.CourseTitle;
```

**C2.** Report the **average grade** per instructor for `2025S1`. Treat non-numeric grades as out of scope; focus on rows where `Grade` IS NOT NULL.

**C3.** Which courses had **zero** enrollments in `2025S1`? Use a LEFT JOIN and a `HAVING COUNT(...) = 0` pattern.

**C4.** Explain the difference between:

- `WHERE e.Semester = '2025S1'` before grouping, and  
- `HAVING COUNT(e.EnrollmentId) > 0` after grouping.

---

## Section D — Join cardinality and result size

**D1.** If table `A` has 3 rows and table `B` has 4 rows, what is the **maximum** number of rows in `A INNER JOIN B` when the join condition is `A.id = B.a_id` and each `B` row references at most one `A` row?

**D2.** When joining `Enrollment` to `Course`, why is the result usually **many enrollments per course** rather than one row per course?

**D3.** Give one example where a **many-to-many** relationship requires an **intermediate table** (like `Enrollment`) before you can join students directly to courses.

---

## Section E — Debugging and tracing

Trace the following query on the sample data. How many rows are returned?

```sql
SELECT s.FullName, c.CourseTitle, i.InstructorName
FROM Enrollment e
INNER JOIN Student s ON e.StudentId = s.StudentId
INNER JOIN Course c ON e.CourseCode = c.CourseCode
LEFT JOIN Instructor i ON e.InstructorId = i.InstructorId
WHERE c.Credits >= 5;
```

**Checklist when a join returns unexpected rows:**

1. Verify foreign-key values exist in parent tables.  
2. Check for NULL keys on optional relationships.  
3. Confirm INNER vs LEFT choice matches the business question.  
4. Move outer-join filters from `WHERE` to `ON` when preserving non-matching rows.

---

## Guided deliverables (submit to instructor)

1. **Query pack:** Solutions for A2, B3, C2, and C3 (one `.sql` file or typed appendix).  
2. **Explanation paragraph:** When you would choose LEFT JOIN over INNER JOIN for reporting "all students and their optional enrollments."  
3. **Diagram:** Draw the join path `Student → Enrollment → Course → Instructor` and label primary/foreign keys.

---

## Reflection and access-aware learning

1. Resource 065 is **CourseOnly**. Why should Bianca (not in `databases-demo-course`) see an empty supplementary-materials list even if she guesses the resource identifier?  
2. After Alex uploads this worksheet, how does the **extracted summary** help the hybrid recommender suggest related items (e.g., normalization reading, SQL practice videos) **without** exposing the full document in every API response?  
3. The catalog lists this item as **OfflinePhysical** with print instructions. What is the benefit of still allowing a **digital upload** for the same catalog entry?

---

## Self-assessment

| I can… | Confident | Needs review |
|--------|-----------|--------------|
| Write INNER JOIN on two tables | ☐ | ☐ |
| Use LEFT JOIN to keep unmatched left rows | ☐ | ☐ |
| Chain three or more tables | ☐ | ☐ |
| Use GROUP BY with COUNT / AVG | ☐ | ☐ |
| Explain NULL behavior in outer joins | ☐ | ☐ |

---

## Connection to adaptive learning

The Educational Companion can attach this seminar worksheet to resource **065** and index a short **extracted text summary** for content-based matching. Alex's completed exercises on joins, together with preferences for the Databases topic, strengthen signals for the **collaborative** and **difficulty** branches (difficulty 2, ~45 minutes). Recommendations remain **access-filtered**: summaries and file metadata inherit the same `databases-demo-course` scope as the parent resource.

*Original thesis demo material — Seminar 4 SQL joins worksheet for the Intelligent Educational Companion System.*
