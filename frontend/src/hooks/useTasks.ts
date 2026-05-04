import { useEffect, useState } from "react";
import { createUserTask, deleteUserTask, getUserTasks, updateUserTask, updateUserTaskStatus, type CreateStudyTaskRequest, type UpdateStudyTaskRequest } from "../services/api";
import type { StudyTask, TaskStatus } from "../types";

type UseTasksState = {
  userId: string | null;
  tasks: StudyTask[];
  loading: boolean;
  error: string | null;
};

export function useTasks(userId: string) {
  const [refreshTick, setRefreshTick] = useState(0);
  const [state, setState] = useState<UseTasksState>({
    userId: null,
    tasks: [],
    loading: true,
    error: null,
  });

  useEffect(() => {
    let cancelled = false;

    getUserTasks(userId)
      .then((tasks) => {
        if (cancelled) return;
        setState({
          userId,
          tasks,
          loading: false,
          error: null,
        });
      })
      .catch(() => {
        if (cancelled) return;
        setState({
          userId,
          tasks: [],
          loading: false,
          error: "Could not load tasks.",
        });
      });

    return () => {
      cancelled = true;
    };
  }, [userId, refreshTick]);

  useEffect(() => {
    const onInteractionUpdated = () => setRefreshTick((x) => x + 1);
    window.addEventListener("interaction-updated", onInteractionUpdated);
    return () => window.removeEventListener("interaction-updated", onInteractionUpdated);
  }, []);

  const refresh = () => setRefreshTick((x) => x + 1);
  const emitTaskUpdated = () => window.dispatchEvent(new Event("task-updated"));

  const setTaskStatus = async (taskId: string, status: TaskStatus) => {
    await updateUserTaskStatus(userId, taskId, status);
    refresh();
    emitTaskUpdated();
  };

  const createTask = async (payload: CreateStudyTaskRequest) => {
    await createUserTask(userId, payload);
    refresh();
    emitTaskUpdated();
  };

  const editTask = async (taskId: string, payload: UpdateStudyTaskRequest) => {
    await updateUserTask(userId, taskId, payload);
    refresh();
    emitTaskUpdated();
  };

  const removeTask = async (taskId: string) => {
    await deleteUserTask(userId, taskId);
    refresh();
    emitTaskUpdated();
  };

  return {
    tasks: state.userId === userId ? state.tasks : [],
    loading: state.userId !== userId || state.loading,
    error: state.userId === userId ? state.error : null,
    refresh,
    setTaskStatus,
    createTask,
    editTask,
    removeTask,
  };
}

