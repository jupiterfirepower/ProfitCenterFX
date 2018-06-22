--insert into dbo.workerpayment values(1,getdate(), 2300)
--insert into dbo.workerpayment values(2,getdate(), 2200)
--insert into dbo.workerpayment values(5,getdate(), 2400)

SELECT w.id, w.name
FROM dbo.worker w
WHERE NOT EXISTS
(SELECT w.id FROM dbo.workerpayment wp WHERE w.id = wp.workerid AND wp.date BETWEEN '2018/06/1' AND '2018/06/30')